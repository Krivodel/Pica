using System.Security.Cryptography;
using System.Text;

using Pica.Protocol;

namespace Pica.Desktop.Services.Background;

internal sealed record PicaBackgroundActivationEndpoint(
    string PipeName,
    string AvailabilityMutexName)
{
    public static PicaBackgroundActivationEndpoint Default { get; } =
        CreateDefault();

    private static PicaBackgroundActivationEndpoint CreateDefault()
    {
        string userScope = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(userScope))
        {
            userScope = Environment.UserName;
        }

        byte[] userScopeHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(userScope));
        string userScopeSuffix = Convert.ToHexString(
            userScopeHash.AsSpan(0, 8));
        string endpointPrefix =
            $"{PicaProtocolConstants.ApplicationName}.BackgroundActivation.{userScopeSuffix}.v1";

        return new PicaBackgroundActivationEndpoint(
            endpointPrefix,
            $"{endpointPrefix}.Available");
    }
}
