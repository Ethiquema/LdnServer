using LanPlayServer.Network.Types;
using LanPlayServer.Utils;
using System.Text.RegularExpressions;

namespace LanPlayServer
{
    public partial class LdnSession
    {
        private void HandlePassphrase(LdnHeader header, PassphraseMessage message)
        {
            string passphrase = StringUtils.ReadUtf8String(message.Passphrase.AsSpan());
            bool   valid      = passphrase == string.Empty || (passphrase.Length == 16 && PassphrasePattern.IsMatch(passphrase));

            Passphrase = valid ? passphrase : string.Empty;
        }

        private static readonly Regex PassphrasePattern = PassphraseRegex();

        [GeneratedRegex("Ryujinx-[0-9a-f]{8}")]
        private static partial Regex PassphraseRegex();
    }
}