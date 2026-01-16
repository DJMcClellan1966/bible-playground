using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace AI_Bible_App.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.828810073591-1chpql2jqhr6s6sfcrrgtkh174uo7flt",
    DataHost = "oauth2redirect",
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
}
