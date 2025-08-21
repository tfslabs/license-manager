using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
#pragma warning disable 169


// ReSharper disable once CheckNamespace
namespace HGM.Hotbird64.LicenseManager
{
    public static partial class NativeMethods
    {

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        public class AuthPrompt : IDisposable
        {
            [SuppressMessage("ReSharper", "InconsistentNaming")]
            private struct CREDUI_INFO
            {
                public int cbSize;
                private IntPtr hwndParent;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string MessageText;
                [MarshalAs(UnmanagedType.LPWStr)]
                public string CaptionText;
                private IntPtr hbmBanner;
            }

            [DllImport("credui", CharSet = CharSet.Unicode)]
            private static extern CredUiReturnCodes CredUIPromptForCredentials(
                  ref CREDUI_INFO creditUr,
                  string targetName,
                  IntPtr reserved1,
                  int iError,
                  StringBuilder userName,
                  int maxUserName,
                  StringBuilder password,
                  int maxPassword,
                  [MarshalAs(UnmanagedType.Bool)] ref bool pfSave,
                  CREDUI_FLAGS flags);

            [DllImport("credui", CharSet = CharSet.Unicode)]
            private static extern CredUiReturnCodes CredUIConfirmCredentials(string targetName, [In, MarshalAs(UnmanagedType.Bool)] bool bConfirm);

            [Flags]
            [SuppressMessage("ReSharper", "InconsistentNaming")]
            enum CREDUI_FLAGS
            {
                INCORRECT_PASSWORD = 0x1,
                DO_NOT_PERSIST = 0x2,
                REQUEST_ADMINISTRATOR = 0x4,
                EXCLUDE_CERTIFICATES = 0x8,
                REQUIRE_CERTIFICATE = 0x10,
                SHOW_SAVE_CHECK_BOX = 0x40,
                ALWAYS_SHOW_UI = 0x80,
                REQUIRE_SMARTCARD = 0x100,
                PASSWORD_ONLY_OK = 0x200,
                VALIDATE_USERNAME = 0x400,
                COMPLETE_USERNAME = 0x800,
                PERSIST = 0x1000,
                SERVER_CREDENTIAL = 0x4000,
                EXPECT_CONFIRMATION = 0x20000,
                GENERIC_CREDENTIALS = 0x40000,
                USERNAME_TARGET_CREDENTIALS = 0x80000,
                KEEP_USERNAME = 0x100000,
            }

            public enum CredUiReturnCodes
            {
                NO_ERROR = 0,
                ERROR_CANCELLED = 1223,
                ERROR_NO_SUCH_LOGON_SESSION = 1312,
                ERROR_NOT_FOUND = 1168,
                ERROR_INVALID_ACCOUNT_NAME = 1315,
                ERROR_INSUFFICIENT_BUFFER = 122,
                ERROR_INVALID_PARAMETER = 87,
                ERROR_INVALID_FLAGS = 1004,
            }


            [SuppressMessage("Microsoft.Security", "CA2122:DoNotIndirectlyExposeMethodsWithLinkDemands")]
            public CredUiReturnCodes PromptForPassword(bool showUI, string messageText, string captionText)
            {
                StringBuilder userPassword = new StringBuilder(256), userID = new StringBuilder(256);

                CREDUI_INFO credUI = new CREDUI_INFO();
                credUI.cbSize = Marshal.SizeOf(credUI);
                credUI.CaptionText = captionText;
                credUI.MessageText = messageText;
                bool save = true;
                CREDUI_FLAGS flags = (showUI ? CREDUI_FLAGS.ALWAYS_SHOW_UI : 0) | CREDUI_FLAGS.GENERIC_CREDENTIALS | CREDUI_FLAGS.SHOW_SAVE_CHECK_BOX | CREDUI_FLAGS.EXPECT_CONFIRMATION;

                CredUiReturnCodes returnCode = CredUIPromptForCredentials(ref credUI, ServerName, IntPtr.Zero, 0, userID, 100, userPassword, 100, ref save, flags);

                User = userID.ToString();

                SecurePassword = new SecureString();
                for (int i = 0; i < userPassword.Length; i++)
                {
                    SecurePassword.AppendChar(userPassword[i]);
                }

                return returnCode;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (SecurePassword != null)
                {
                    if (disposing)
                    {
                        SecurePassword.Dispose();
                    }

                    SecurePassword = null;
                }
            }

            public CredUiReturnCodes ConfirmCredentials(bool correct)
            {
                return CredUIConfirmCredentials(ServerName, correct);
            }

            public string User { get; private set; }

            public SecureString SecurePassword { get; private set; }

            public string ServerName;
        }
    }
}
