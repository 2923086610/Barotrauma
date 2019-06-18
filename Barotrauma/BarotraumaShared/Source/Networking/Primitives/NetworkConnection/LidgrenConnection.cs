﻿using System;
using System.Net;

namespace Barotrauma.Networking
{
    public class LidgrenConnection : NetworkConnection
    {
        public IPEndPoint IPEndPoint { get; private set; }
        
        public string IP
        {
            get
            {
                return IPEndPoint.Address.IsIPv4MappedToIPv6 ? IPEndPoint.Address.MapToIPv4().ToString() : IPEndPoint.Address.ToString();
            }
        }

        public UInt64 SteamID
        {
            get;
            private set;
        }

        public UInt16 Port
        {
            get
            {
                return (UInt16)IPEndPoint.Port;
            }
        }

        public LidgrenConnection(string ip, UInt64 steamId, UInt16 port)
        {
            IPEndPoint = new IPEndPoint(IPAddress.Parse(ip), (int)port);
            SteamID = steamId;
        }

        public override void Disconnect(string reason)
        {
            throw new NotImplementedException();
        }

        public override void Ban(string reason, TimeSpan? duration)
        {
            throw new NotImplementedException();
        }
    }
}
