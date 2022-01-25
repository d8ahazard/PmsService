using System;
using System.Management;
using Serilog;


namespace PlexServiceCommon;

public class ProcessWatcher {

	private string _processName;
	public ProcessWatcher(string processName) {
		_processName = processName;
		var startWatch = new ManagementEventWatcher(
			new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
		startWatch.EventArrived += startWatch_EventArrived;
		startWatch.Start();
		var stopWatch = new ManagementEventWatcher(
			new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
		stopWatch.EventArrived += stopWatch_EventArrived;
		stopWatch.Start();
		Console.WriteLine("Press any key to exit");
		while (!Console.KeyAvailable) System.Threading.Thread.Sleep(50);
		startWatch.Stop();
		stopWatch.Stop();	
	}

	private void stopWatch_EventArrived(object sender, EventArrivedEventArgs e) {
		if (e.NewEvent.Properties["ProcessName"].Value.ToString() == _processName) {
			Log.Debug("Process stopped: " + e.NewEvent.Properties["ProcessName"].Value);
		}
		Console.WriteLine("Process stopped: {0}", e.NewEvent.Properties["ProcessName"].Value);
	}

	private void startWatch_EventArrived(object sender, EventArrivedEventArgs e) {
		if (e.NewEvent.Properties["ProcessName"].Value.ToString() == _processName) {
			Log.Debug("Process started: " + e.NewEvent.Properties["ProcessName"].Value);
		}
		Console.WriteLine("Process started: {0}", e.NewEvent.Properties["ProcessName"].Value);
	}
}	


