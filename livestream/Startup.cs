using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using LazyCache;
using LivestreamFunctions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;

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
            var jwksEndpoint = Configuration["JwksEndpoint"];

            var awsAccessKey = Configuration["AWSAccessKey"];
            var awsAccessKeySecret = Configuration["AWSAccessKeySecret"];
            var s3KeyBucketName = Configuration["S3KeyBucketName"];
            var dashKeyGroup = Configuration["DASHKeyGroup"];

            services.AddHttpClient();
            services.AddLazyCache();
            services.AddSingleton<StreamingTokenHelper>(_ => new StreamingTokenHelper(jwtVerificationKey));
            services.AddSingleton<OAuthValidator>(s => new OAuthValidator(s.GetRequiredService<IAppCache>(), s.GetRequiredService<IHttpClientFactory>(), oidcAuthority, jwksEndpoint));
            services.AddSingleton<KeyRepository>(s => new KeyRepository(s.GetRequiredService<IAppCache>(), s.GetRequiredService<IAmazonS3>(), s3KeyBucketName, dashKeyGroup));

            var awsCredentials = new BasicAWSCredentials(awsAccessKey, awsAccessKeySecret);
            services.AddSingleton<IAmazonS3>((s) => new AmazonS3Client(awsCredentials, RegionEndpoint.EUNorth1));
            services.AddCors(options => {
                // TODO: What is the correct CORS setup for this functions?
                options.AddPolicy("All",
                builder => {
                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthorization();
            app.UseCors();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllers();
            });
        }
    }
}
