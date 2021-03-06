using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using LazyCache;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using VODStreaming.Model;
using VODStreaming.Services;

namespace VODStreaming
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Note that this config can come from your user secrets for development or from the environment
            var jwtVerificationKey = Configuration["BrunstadTVJWTVerificationKey"];
            var oidcAuthority = Configuration["OidcAuthority"];

            var awsAccessKey = Configuration["AWSAccessKey"];
            var awsAccessKeySecret = Configuration["AWSAccessKeySecret"];
            var s3KeyBucketName = Configuration["S3KeyBucketName"];
            var dashKeyGroup = Configuration["DASHKeyGroup"];

            var storageConnectionString = Configuration.GetConnectionString("AzureStorage");

            var vodOptions = new VODOptions();
            services.AddOptions<VODOptions>().Bind(Configuration.GetSection(VODOptions.ConfigurationSection));
            services.AddControllers();
            services.AddHttpClient("manifests").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 20
            });
            services.ConfigureTelemetryModule<EventCounterCollectionModule>(
                (module, o) => {
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-thread-count"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-queue-length"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-completed-items-count"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-0-gc-count"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-1-gc-count"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-2-gc-count"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-fragmentation"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.NameResolution", "dns-lookups-duration"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Sockets", "outgoing-connections-established"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Sockets", "incoming-connections-established"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "current-requests"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started"));
                    module.Counters.Add(new EventCounterCollectionRequest("System.Net.Http", "requests-started-rate"));
                }
            );

#if !DEBUG || true
            services.AddLazyCache();
#else
            services.AddSingleton<IAppCache, FakeAppCache>();
#endif
            services.AddSingleton<HlsProxyService>();
            services.AddSingleton(s => new SubtitleService(storageConnectionString, s.GetRequiredService<IAppCache>()));
            services.AddLogging();

            var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsAccessKeySecret);
            services.AddSingleton<IAmazonS3>((s) => new AmazonS3Client(awsCredentials, RegionEndpoint.EUNorth1));
            services.AddCors(options => {
                options.AddPolicy("All", builder => {
                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            services.AddAuthorization(options => {
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => {
                options.Authority = Configuration["OidcAuthority"];
                options.TokenValidationParameters.ValidateAudience = false;
            });

            services.Configure<ForwardedHeadersOptions>(options => {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddApplicationInsightsTelemetry(options => {
                options.ConnectionString = Configuration.GetConnectionString("ApplicationInsights");
                options.EnableAdaptiveSampling = false;
            });
            services.AddSingleton<ITelemetryInitializer, RemoveTokensTelemetryInitializer>();
            services.AddSingleton<ITelemetryInitializer, UserAgentTelemetryInitializer>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, TelemetryConfiguration telemetryConfiguration)
        {
            telemetryConfiguration.DefaultTelemetrySink.TelemetryProcessorChainBuilder
                .UseAdaptiveSampling(maxTelemetryItemsPerSecond: 2)
                .Build();

            app.UseForwardedHeaders();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }
    }
}
