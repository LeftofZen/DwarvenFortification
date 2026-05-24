using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DwarvenFortification.Mcp
{
	/// <summary>
	/// Hosts an embedded ASP.NET Core Kestrel server on localhost exposing the simulation over
	/// the Model Context Protocol (HTTP/SSE transport). MCP clients connect to
	/// <c>http://localhost:{port}</c> and invoke the tools declared in
	/// <see cref="SimulationInspectionTools"/>. The host runs on a background task so the
	/// MonoGame loop is unaffected.
	/// </summary>
	public sealed class SimulationMcpHost : IAsyncDisposable
	{
		readonly WebApplication app;

		SimulationMcpHost(WebApplication app)
		{
			this.app = app;
		}

		public static SimulationMcpHost Start(SimulationSnapshotProvider snapshots, int port = 7421)
		{
			var builder = WebApplication.CreateSlimBuilder();
			builder.WebHost.UseUrls($"http://localhost:{port}");
			builder.Logging.ClearProviders();
			builder.Logging.SetMinimumLevel(LogLevel.Warning);

			builder.Services.AddSingleton(snapshots);
			builder.Services
				.AddMcpServer()
				.WithHttpTransport()
				.WithToolsFromAssembly(typeof(SimulationMcpHost).Assembly);

			var app = builder.Build();
			app.MapMcp();

			// Fire and forget: Kestrel runs on its own pipeline; the MonoGame loop is untouched.
			_ = app.RunAsync();

			return new SimulationMcpHost(app);
		}

		public async ValueTask DisposeAsync()
		{
			try
			{
				await app.StopAsync(TimeSpan.FromSeconds(2));
			}
			catch
			{
				// best effort during shutdown
			}

			await app.DisposeAsync();
		}
	}
}
