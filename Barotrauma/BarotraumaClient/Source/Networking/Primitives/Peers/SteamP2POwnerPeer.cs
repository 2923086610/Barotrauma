﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Lidgren.Network;
using Facepunch.Steamworks;
using Barotrauma.Steam;
using System.Linq;

namespace Barotrauma.Networking
{
    class SteamP2POwnerPeer : ClientPeer
    {
        private NetClient netClient;
        private NetPeerConfiguration netPeerConfiguration;

        private ConnectionInitialization initializationStep;
        private int passwordSalt;
        private Auth.Ticket steamAuthTicket;
        List<NetIncomingMessage> incomingLidgrenMessages;

        public SteamP2POwnerPeer(string name)
        {
            ServerConnection = null;

            Name = name;

            netClient = null;
        }

        public override void Start(object endPoint)
        {
            if (netClient != null) { return; }

            netPeerConfiguration = new NetPeerConfiguration("barotrauma");

            netPeerConfiguration.DisableMessageType(NetIncomingMessageType.DebugMessage | NetIncomingMessageType.WarningMessage | NetIncomingMessageType.Receipt
                | NetIncomingMessageType.ErrorMessage | NetIncomingMessageType.Error);

            netClient = new NetClient(netPeerConfiguration);

            steamAuthTicket = SteamManager.GetAuthSessionTicket();
            //TODO: wait for GetAuthSessionTicketResponse_t

            if (steamAuthTicket == null)
            {
                throw new Exception("GetAuthSessionTicket returned null");
            }

            incomingLidgrenMessages = new List<NetIncomingMessage>();

            initializationStep = ConnectionInitialization.SteamTicketAndVersion;

            if (!(endPoint is IPEndPoint ipEndPoint))
            {
                throw new InvalidCastException("endPoint is not IPEndPoint");
            }
            if (ServerConnection != null)
            {
                throw new InvalidOperationException("ServerConnection is not null");
            }

            netClient.Start();
            ServerConnection = new LidgrenConnection("Server", netClient.Connect(ipEndPoint), 0);
        }

        public override void Update()
        {
            if (netClient == null) { return; }

            netClient.ReadMessages(incomingLidgrenMessages);

            foreach (NetIncomingMessage inc in incomingLidgrenMessages)
            {
                if (inc.SenderConnection != (ServerConnection as LidgrenConnection).NetConnection) { continue; }

                switch (inc.MessageType)
                {
                    case NetIncomingMessageType.Data:
                        HandleDataMessage(inc);
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        HandleStatusChanged(inc);
                        break;
                }
            }

            incomingLidgrenMessages.Clear();
        }

        private void HandleDataMessage(NetIncomingMessage inc)
        {
            if (netClient == null) { return; }

            throw new NotImplementedException();
        }

        private void HandleStatusChanged(NetIncomingMessage inc)
        {
            if (netClient == null) { return; }

            NetConnectionStatus status = (NetConnectionStatus)inc.ReadByte();
            switch (status)
            {
                case NetConnectionStatus.Disconnected:
                    string disconnectMsg = inc.ReadString();
                    Close(disconnectMsg);
                    break;
            }
        }

        private void ReadConnectionInitializationStep(NetIncomingMessage inc)
        {
            if (netClient == null) { return; }

            ConnectionInitialization step = (ConnectionInitialization)inc.ReadByte();
            //DebugConsole.NewMessage(step + " " + initializationStep);
            switch (step)
            {
                case ConnectionInitialization.SteamTicketAndVersion:
                    if (initializationStep != ConnectionInitialization.SteamTicketAndVersion) { return; }
                    NetOutgoingMessage outMsg = netClient.CreateMessage();
                    outMsg.Write((byte)PacketHeader.IsConnectionInitializationStep);
                    outMsg.Write((byte)ConnectionInitialization.SteamTicketAndVersion);
                    outMsg.Write(Name);
                    outMsg.Write(SteamManager.GetSteamID());
                    outMsg.Write((UInt16)steamAuthTicket.Data.Length);
                    outMsg.Write(steamAuthTicket.Data, 0, steamAuthTicket.Data.Length);

                    outMsg.Write(GameMain.Version.ToString());

                    IEnumerable<ContentPackage> mpContentPackages = GameMain.SelectedPackages.Where(cp => cp.HasMultiplayerIncompatibleContent);
                    outMsg.WriteVariableInt32(mpContentPackages.Count());
                    foreach (ContentPackage contentPackage in mpContentPackages)
                    {
                        outMsg.Write(contentPackage.Name);
                        outMsg.Write(contentPackage.MD5hash.Hash);
                    }

                    netClient.SendMessage(outMsg, NetDeliveryMethod.ReliableUnordered);
                    break;
                case ConnectionInitialization.Password:
                    if (initializationStep == ConnectionInitialization.SteamTicketAndVersion) { initializationStep = ConnectionInitialization.Password; }
                    if (initializationStep != ConnectionInitialization.Password) { return; }
                    bool incomingSalt = inc.ReadBoolean(); inc.ReadPadBits();
                    int retries = 0;
                    if (incomingSalt)
                    {
                        passwordSalt = inc.ReadInt32();
                    }
                    else
                    {
                        retries = inc.ReadInt32();
                    }
                    OnRequestPassword?.Invoke(passwordSalt, retries);
                    break;
            }
        }

        public override void SendPassword(string password)
        {
            if (netClient == null) { return; }

            if (initializationStep != ConnectionInitialization.Password) { return; }
            NetOutgoingMessage outMsg = netClient.CreateMessage();
            outMsg.Write((byte)PacketHeader.IsConnectionInitializationStep);
            outMsg.Write((byte)ConnectionInitialization.Password);
            byte[] saltedPw = ServerSettings.SaltPassword(NetUtility.ComputeSHAHash(Encoding.UTF8.GetBytes(password)), passwordSalt);
            outMsg.Write((byte)saltedPw.Length);
            outMsg.Write(saltedPw, 0, saltedPw.Length);
            netClient.SendMessage(outMsg, NetDeliveryMethod.ReliableUnordered);
        }

        public override void Close(string msg = null)
        {
            if (netClient == null) { return; }

            netClient.Shutdown(msg ?? TextManager.Get("Disconnecting"));
            steamAuthTicket?.Cancel(); steamAuthTicket = null;
            OnDisconnect?.Invoke(msg);
            netClient = null;
        }

        public override void Send(IWriteMessage msg, DeliveryMethod deliveryMethod)
        {
            if (netClient == null) { return; }

            NetDeliveryMethod lidgrenDeliveryMethod = NetDeliveryMethod.Unreliable;
            switch (deliveryMethod)
            {
                case DeliveryMethod.Unreliable:
                    lidgrenDeliveryMethod = NetDeliveryMethod.Unreliable;
                    break;
                case DeliveryMethod.Reliable:
                    lidgrenDeliveryMethod = NetDeliveryMethod.ReliableUnordered;
                    break;
                case DeliveryMethod.ReliableOrdered:
                    lidgrenDeliveryMethod = NetDeliveryMethod.ReliableOrdered;
                    break;
            }

            NetOutgoingMessage lidgrenMsg = netClient.CreateMessage();
            byte[] msgData = new byte[1500];
            bool isCompressed; int length;
            msg.PrepareForSending(msgData, out isCompressed, out length);
            lidgrenMsg.Write((byte)(isCompressed ? PacketHeader.IsCompressed : PacketHeader.None));
            lidgrenMsg.Write((UInt16)length);
            lidgrenMsg.Write(msgData, 0, length);

            netClient.SendMessage(lidgrenMsg, lidgrenDeliveryMethod);
        }
    }
}
