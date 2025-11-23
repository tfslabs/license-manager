using HGM.Hotbird64.Vlmcs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public static partial class NativeMethods
    {
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern IntPtr LoadLibraryW(string dllFileName);
    }

    public partial class App
    {
        public static readonly Brush DefaultTextBoxBackground = new SolidColorBrush(Color.FromRgb(0xf0, 0xf0, 0xf0));
        public const double ZoomFactor = 1.025;
        public static bool HaveLibKms, IsLibKmsLoadError;
        public static string DatabaseFileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "KmsDataBase.xml");
        public const string GuidPattern = @"^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12})|(\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-4[0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\})$";
        public static readonly IReadOnlyList<KmsGuid> ServerKmsGuids;
        public static event Action DataBaseLoaded;
        public static readonly string ExeDirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static IReadOnlyList<Brush> DatagridBackgroundBrushes =
        [
            new SolidColorBrush(new Color { A = 255, R = 255, B = 255, G = 255 }),
            new SolidColorBrush(new Color { A = 255, R = 240, B = 255, G = 248 }),
        ];

        public static bool IsDatabaseLoaded;


        static App()
        {
            ServerKmsGuids = [
                new("33e156e4-b76f-4a52-9f91-f641dd95ac48"), //Windows Server 2008 A
                new("8fe53387-3087-4447-8985-f75132215ac9"), //Windows Server 2008 B
                new("8a21fdf3-cbc5-44eb-83f3-fe284e6680a7"), //Windows Server 2008 C
                new("0fc6ccaf-ff0e-4fae-9d08-4370785bf7ed"), //Windows Server 2008 R2 A
                new("ca87f5b6-cd46-40c0-b06d-8ecd57a4373f"), //Windows Server 2008 R2 B
                new("b2ca2689-a9a8-42d7-938d-cf8e9f201958"), //Windows Server 2008 R2 C
                new("8665cb71-468c-4aa3-a337-cb9bc9d5eaac"), //Windows Server 2012
                new("8456efd3-0c04-4089-8740-5b7238535a65"), //Windows Server 2012 R2
                new("6e9fc069-257d-4bc4-b4a7-750514d32743"), //Windows Server 2016
                new("8449b1fb-f0ea-497a-99ab-66ca96e9a0f5"), //Windows Server 2019
                new("b74263e4-0f92-46c6-bcf8-c11d5efe2959"), //Windows Server 2022
                new("907f1f65-adcd-4a2e-95bc-4bf500bc6e58")  //Windows Server 2025
            ];
        }

        private static bool TryLoadLibrary(string dllFileName)
        {
            if (NativeMethods.LoadLibraryW(dllFileName) != IntPtr.Zero)
            {
                return true;
            }

            int error = Marshal.GetLastWin32Error();

            if (error == 126)
            {
                return false;
            }

            Win32Exception ex = new(error);
            string message = ex.Message.Replace("%1", dllFileName);
            throw new Win32Exception(error, message);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                TapMirror.Stop();
                if (ProductBrowser.PKeyConfigFiles == null) { return; }

                foreach (PKeyConfigFile file in ProductBrowser.PKeyConfigFiles)
                {
                    if (!file.IsOnFileSystem || file.IsUnzippedExternal)
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(file.TempFileName);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            finally
            {
                base.OnExit(e);
            }
        }

        private void AppUI_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show
            (
#if DEBUG
        e.Exception == null ? "An unknown error has occured." : $"{e.Exception}",
#else
        e.Exception == null ? "An unknown error has occured." : $"{e.Exception.GetType().Name}: {e.Exception.Message}",
#endif
        "License Manager Error",
              MessageBoxButton.OK,
              MessageBoxImage.Error
            );

            e.Handled = true;
        }

        public static void LoadLibKms(string fileName)
        {
            try
            {
                HaveLibKms = TryLoadLibrary(fileName);
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(ex.Message, $"Error Loading \"{fileName}\"", MessageBoxButton.OK, MessageBoxImage.Error);
                HaveLibKms = false;
                IsLibKmsLoadError = true;
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if
            (
                Clipboard.ContainsText() &&
                Regex.IsMatch(Clipboard.GetText().ToUpperInvariant(), BinaryProductKey.KeyPattern) &&
                MessageBox.Show
                (
                    "There is a Product Key in the Windows Clipboard." + Environment.NewLine +
                    "Do you want to clear the Clipboard?",
                    "License Manager Privacy Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes
                ) == MessageBoxResult.Yes
            )
            {
                Clipboard.Clear();
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            LoadLibKms($"libkms{IntPtr.Size << 3}.dll");

            KmsLists.LoadDatabase = () =>
            {
                if (IsDatabaseLoaded)
                {
                    return;
                }

                try
                {
                    using FileStream stream = new(DatabaseFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    KmsLists.ReadDatabase(stream);
                    IsDatabaseLoaded = true;
                    DataBaseLoaded?.Invoke();
                }
                catch (Exception ex)
                {
                    if (!(ex is FileNotFoundException || ex.InnerException is FileNotFoundException))
                    {
                        MessageBox.Show
                  (
                    $"The KMS database \"{DatabaseFileName}\" could not be read:\n{ex.Message}\n\nUsing License Manager's built-in database",
                    $"Could not read {Path.GetFileName(DatabaseFileName)}", MessageBoxButton.OK, MessageBoxImage.Error
                  );
                    }
                    using Stream stream = GetResourceStream(new Uri("pack://application:,,,/LmInternalDatabase.xml"))?.Stream;
                    KmsLists.ReadDatabase(stream);
                    IsDatabaseLoaded = true;
                    DataBaseLoaded?.Invoke();
                }
            };

            KmsLists.GetXsdValidationStream = () => GetResourceStream(new Uri("pack://application:,,,/KmsDataBase.xsd"))?.Stream;
        }
    }
}
