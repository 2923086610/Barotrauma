﻿using Barotrauma.Networking;

namespace Barotrauma.Items.Components
{
    partial class Repairable : ItemComponent, IServerSerializable, IClientSerializable
    {
        void InitProjSpecific()
        {
            //let the clients know the initial deterioration delay
            item.CreateServerEvent(this);
        }

        public void ServerRead(ClientNetObject type, IReadMessage msg, Client c)
        {
            if (c.Character == null) return;
            StartRepairing(c.Character);
        }

        public void ServerWrite(IWriteMessage msg, Client c, object[] extraData = null)
        {
            msg.Write(deteriorationTimer);
        }
    }
}
