using Avalonia.Threading;

namespace LogViewer.Avalonia.Logging
{
	public class LogDataStore : Core.LogDataStore
	{
	    #region Methods

	    public override async void AddEntry(Core.LogModel logModel)
	        => await Dispatcher.UIThread.InvokeAsync(() => base.AddEntry(logModel));

	    #endregion
	}
}