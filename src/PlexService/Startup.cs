#region

//using System;
//using System.IO;
//using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using PlexService.Hubs;
using PlexService.Models;

#endregion

namespace PlexService {
	public class Startup {
		// This method gets called by the runtime. Use this method to add services to the container.
		public static void ConfigureServices(IServiceCollection services) {
			services.AddControllersWithViews();
			services.AddControllers()
				.AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; })
				.AddNewtonsoftJson();
			var settings = new JsonSerializerSettings { ContractResolver = new SignalRContractResolver() };
			var serializer = JsonSerializer.Create(settings);
			services.AddSingleton(serializer);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public static void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			} else {
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			//app.UseSwagger();
			//app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1.3/swagger.json", "My API V1"); });
			app.UseRouting();
			app.UseAuthorization();
			app.UseEndpoints(endpoints => {
				endpoints.MapControllerRoute(
					"default",
					"{controller=Home}/{action=Index}/{id?}");
				endpoints.MapHub<SocketServer>("/socket");
			});
			app.Use(async (context, next) => {
				var unused = context.RequestServices
					.GetRequiredService<IHubContext<SocketServer>>();

				if (next != null) {
					await next.Invoke();
				}
			});
		}
	}
}