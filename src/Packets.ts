
import dgram = require('dgram');
import Entity = require('./Entity');
var entIdLength = 2;
var graphicLength = 2;
var floatLength = 4;

// 2 bytes protocol
// 2 bytes type code
// N bytes data
export class GamePacket {
  private static protocolBuffer = new Buffer([0xD8, 0x2A]);
  private static protocolLength = GamePacket.protocolBuffer.length;
  private static typeLength = 2;
  private static dataStart = GamePacket.protocolBuffer.length + GamePacket.typeLength;
  private buffer: Buffer;
  constructor(buf: Buffer) {
    this.buffer = buf;
    this.checkProtocol();
  }

  private checkProtocol() {
    var protocolBuf = this.buffer.slice(0, GamePacket.protocolLength);
    if (protocolBuf.equals(GamePacket.protocolBuffer)) {
      return;
    } else {
      console.log(this.buffer);
      throw "Protocol doesn't match: " + this.buffer;
    }
  }

  getTypeBuffer(): Buffer {
    return this.buffer.slice(GamePacket.protocolLength, GamePacket.dataStart)
  }

  getDataBuffer(): Buffer {
    return this.buffer.slice(GamePacket.dataStart);
  }

  sortPacket(listener: PacketListener, remote: dgram.RemoteInfo) {
    if (this.getTypeBuffer().equals(PlayerUpdate.typeBuffer)) {
      listener.onPlayerUpdate(new PlayerUpdate(this.getDataBuffer()), remote);
    } else {
      console.log(this.getTypeBuffer());
      throw "Packet not recognized in sorting.";
    }
  }

  static createPacket(typeBuffer: Buffer, dataBuffer: Buffer): Buffer {
    var buf = new Buffer(GamePacket.protocolLength+GamePacket.typeLength+dataBuffer.length);
    GamePacket.protocolBuffer.copy(buf, 0);
    typeBuffer.copy(buf, GamePacket.protocolLength);
    dataBuffer.copy(buf, GamePacket.dataStart);
    return buf;
  }
}

export interface PacketListener {
  onPlayerUpdate(packet: PlayerUpdate, remote: dgram.RemoteInfo);
}

export class PlayerUpdate {
  // x: 32-bit float
  // y: 32-bit float
  // up-down-angle: 32-bit float
  // left-right-angle: 32-bit float
  // tilt angle: 32-bit float
  static typeBuffer = new Buffer([0x10, 0x00]);
  private databuffer: Buffer;
  private static xIndex = floatLength * 0;
  private static yIndex = floatLength * 1;
  private static upDownAngleIndex = floatLength * 2;
  private static leftRightAngleIndex = floatLength * 3;
  private static tiltAngleIndex = floatLength * 4;

  constructor(buf: Buffer) {
    this.databuffer = buf;
  }
  getX(): number {
    return this.databuffer.readFloatBE(PlayerUpdate.xIndex);
  }
  getY(): number {
    return this.databuffer.readFloatBE(PlayerUpdate.yIndex);
  }
  getUpDownAngle(): number {
    return this.databuffer.readFloatBE(PlayerUpdate.upDownAngleIndex);
  }
  getLeftRightAngle(): number {
    return this.databuffer.readFloatBE(PlayerUpdate.leftRightAngleIndex);
  }
  getTiltAngle(): number {
    return this.databuffer.readFloatBE(PlayerUpdate.tiltAngleIndex);
  }
}

export class EntityUpdate {
  // x: 32-bit float
  // y: 32-bit float
  // up-down-angle: 32-bit float
  // left-right-angle: 32-bit float
  // entid: 16-bit uint
  // graphic: 16-bit uint
  static typeBuffer = new Buffer([0x10, 0x01]);
  private databuffer: Buffer;
  private static xIndex = floatLength * 0;
  private static yIndex = floatLength * 1;
  private static upDownAngleIndex = floatLength * 2;
  private static leftRightAngleIndex = floatLength * 3;
  private static tiltAngleIndex = floatLength * 4;
  private static entIdIndex = floatLength * 5;
  private static graphicIndex = EntityUpdate.entIdIndex + entIdLength;

  constructor(buf: Buffer) {
    this.databuffer = buf;
  }
  getX(): number {
    return this.databuffer.readFloatBE(EntityUpdate.xIndex);
  }
  getY(): number {
    return this.databuffer.readFloatBE(EntityUpdate.yIndex);
  }
  getUpDownAngle(): number {
    return this.databuffer.readFloatBE(EntityUpdate.upDownAngleIndex);
  }
  getLeftRightAngle(): number {
    return this.databuffer.readFloatBE(EntityUpdate.leftRightAngleIndex);
  }
  getTiltAngle(): number {
    return this.databuffer.readFloatBE(EntityUpdate.tiltAngleIndex);
  }
  getID(): number {
    return this.databuffer.readUInt16BE(EntityUpdate.entIdIndex);
  }
  getGraphic(): number {
    return this.databuffer.readUInt16BE(EntityUpdate.graphicIndex);
  }
  public static createPacket(ent: Entity.Entity) : Buffer{
    var buf = new Buffer(EntityUpdate.graphicIndex+graphicLength);
    buf.writeFloatBE(ent.x, EntityUpdate.xIndex);
    buf.writeFloatBE(ent.y, EntityUpdate.yIndex);
    buf.writeFloatBE(ent.upDownAngle, EntityUpdate.upDownAngleIndex);
    buf.writeFloatBE(ent.leftRightAngle, EntityUpdate.leftRightAngleIndex);
    buf.writeFloatBE(ent.tiltAngle, EntityUpdate.tiltAngleIndex);
    buf.writeUInt16BE(ent.id, EntityUpdate.entIdIndex);
    buf.writeUInt16BE(ent.graphic, EntityUpdate.graphicIndex);
    return GamePacket.createPacket(EntityUpdate.typeBuffer, buf);
  }
}
