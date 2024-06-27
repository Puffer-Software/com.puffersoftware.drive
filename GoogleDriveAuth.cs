using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace PufferSoftware.Aurora.Drive
{
    public static class GoogleDriveAuth
    {
        private static readonly string[] Scopes = { Google.Apis.Drive.v3.DriveService.Scope.Drive };
        public const string ApplicationName = "Aurora Drive Tool";

        public static UserCredential GetUserCredential()
        {
            using FileStream stream = new ("client_secret.json", FileMode.Open, FileAccess.Read);
            const string credPath = "token.json";
            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;

            return credential;
        }
    }
}