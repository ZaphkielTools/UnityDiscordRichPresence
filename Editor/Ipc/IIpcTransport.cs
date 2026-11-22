using System;

namespace Zaphkiel.DiscordRichPresence
{
    internal interface IIpcTransport : IDisposable
    {
        void Write(IpcFrame frame);
        bool TryRead(out IpcFrame frame);
    }
}
