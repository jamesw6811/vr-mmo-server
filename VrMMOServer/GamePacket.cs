using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VrMMOServer
{
    public abstract class GamePacket
    {
        public void write(NetworkDataWriter ndw)
        {
            ndw.writeUInt16(getPacketTypeCode());
            writePacketInfo(ndw);
        }

        public Boolean reliable = false;
        abstract protected void writePacketInfo(NetworkDataWriter ndw);
        abstract protected UInt16 getPacketTypeCode();
    }

    public class PingPacket : GamePacket
    {
        public const UInt16 packet_type = 0x0000;

        public static PingPacket fromData(NetworkDataReader ndr)
        {
            return new PingPacket();
        }

        protected override UInt16 getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            return;
        }
    }

    public class EntityRemovePacket : GamePacket
    {
        public const UInt16 packet_type = 0x0002;
        public UInt32 id;
        public static EntityRemovePacket fromData(NetworkDataReader ndr)
        {
            EntityRemovePacket erp = new EntityRemovePacket();
            erp.id = ndr.getUInt32();
            return erp;
        }

        protected override ushort getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            ndw.writeUInt32(id);
        }

        internal static EntityRemovePacket fromEntity(GameEntity ge)
        {
            EntityRemovePacket erp = new EntityRemovePacket();
            erp.id = ge.id;
            erp.reliable = true;
            return erp;
        }
    }

    public class EntityUpdatePacket : GamePacket
    {
        public Single x;
        public Single y;
        public Single upDown;
        public Single leftRight;
        public Single tilt;
        public UInt32 id;
        public UInt32 graphic;
        public const UInt16 packet_type = 0x0001;
        public static EntityUpdatePacket fromData(NetworkDataReader ndr)
        {
            EntityUpdatePacket eup = new EntityUpdatePacket();
            eup.x = ndr.getSingle();
            eup.y = ndr.getSingle();
            eup.upDown = ndr.getSingle();
            eup.leftRight = ndr.getSingle();
            eup.tilt = ndr.getSingle();
            eup.id = ndr.getUInt32();
            eup.graphic = ndr.getUInt32();
            return eup;
        }

        protected override UInt16 getPacketTypeCode()
        {
            return packet_type;
        }

        protected override void writePacketInfo(NetworkDataWriter ndw)
        {
            ndw.writeSingle(x);
            ndw.writeSingle(y);
            ndw.writeSingle(upDown);
            ndw.writeSingle(leftRight);
            ndw.writeSingle(tilt);
            ndw.writeUInt32(id);
            ndw.writeUInt32(graphic);
        }

        public override string ToString()
        {
            return "EntityUpdatePacket:\r\n"
                + " (" + x.ToString() + "," + y.ToString() + ")\r\n"
                + " (" + upDown.ToString() + "," + leftRight.ToString() + "," + tilt.ToString() + ")\r\n"
                + " (" + id.ToString() + "," + graphic.ToString() + ")";
        }

        public void update(GameEntity ge)
        {
            ge.x = x;
            ge.y = y;
            ge.upDown = upDown;
            ge.leftRight = leftRight;
            ge.tilt = tilt;
            ge.graphic = graphic;
        }

        internal static EntityUpdatePacket fromEntity(GameEntity ge)
        {
            EntityUpdatePacket eup = new EntityUpdatePacket();
            eup.x = ge.x;
            eup.y = ge.y;
            eup.graphic = ge.graphic;
            eup.id = ge.id;
            eup.leftRight = ge.leftRight;
            eup.upDown = ge.upDown;
            eup.tilt = ge.tilt;
            return eup;
        }
    }
}
