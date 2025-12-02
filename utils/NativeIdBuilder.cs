using System;

namespace Betekk.RevitXmiExporter.utils
{
    public static class SessionUuidBuilder
    {
        private static string _sessionUuid;

        public static string SessionUuid
        {
            get
            {
                if (_sessionUuid == null)
                {
                    _sessionUuid = Guid.NewGuid().ToString();
                }

                return _sessionUuid;
            }
        }
    }
}
