using System.Reflection;
using System.Windows.Media.Imaging;

namespace PlexServiceTray; 

public static class IconHelper {
	public static BitmapImage? GetIcon() {
		var assembly = Assembly.GetExecutingAssembly();
		var resource = assembly.GetManifestResourceStream("PlexServiceTray.PlexService.ico");
		if (resource == null) {
			return null;
		}
		var bitmapImage = new BitmapImage {
			CreateOptions = BitmapCreateOptions.PreservePixelFormat
		};
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = resource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		resource.Close();
		return bitmapImage;
	}
	
	
}