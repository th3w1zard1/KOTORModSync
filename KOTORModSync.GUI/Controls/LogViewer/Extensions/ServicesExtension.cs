using LogViewer.Core;
using LogViewer.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LogDataStore = LogViewer.Avalonia.Logging.LogDataStore;

namespace LogViewer.Avalonia
{
	public static class ServicesExtension
	{
	    public static HostApplicationBuilder AddLogViewer(this HostApplicationBuilder builder)
	    {
	        builder.Services.AddSingleton<ILogDataStore, LogDataStore>();
	        builder.Services.AddSingleton<LogViewerControlViewModel>();

	        return builder;
	    }
	}
}