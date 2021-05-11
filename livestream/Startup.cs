using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using LazyCache;
using LivestreamFunctions.Model;
using LivestreamFunctions.Services;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LivestreamFunctions
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

            // Note that this config can come from your user secret for development or from the environment
            var jwtVerificationKey = Configuration["BrunstadTVJWTVerificationKey"];
            var oidcAuthority = Configuration["OidcAuthority"];

            var awsAccessKey = Configuration["AWSAccessKey"];
            var awsAccessKeySecret = Configuration["AWSAccessKeySecret"];
            var s3KeyBucketName = Configuration["S3KeyBucketName"];
            var dashKeyGroup = Configuration["DASHKeyGroup"];

            var livestreamOptions = new LivestreamOptions();
            services.AddOptions<LivestreamOptions>().Bind(Configuration.GetSection(LivestreamOptions.ConfigurationSection));
            services.AddControllers();
            services.AddHttpClient();
            services.AddLazyCache();
            services.AddSingleton(_ => new StreamingTokenHelper(jwtVerificationKey));
            services.AddSingleton(s => new KeyRepository(s.GetRequiredService<IAppCache>(), s.GetRequiredService<IAmazonS3>(), s3KeyBucketName, dashKeyGroup));
            services.AddSingleton<HlsProxyService>();
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

            services.Configure<ForwardedHeadersOptions>(options =>
            {
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
