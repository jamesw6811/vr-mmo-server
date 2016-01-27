import Packets = require('./Packets');
export class Entity{
  public x: number;
  public y: number;
  public leftRightAngle: number;
  public upDownAngle: number;
  public tiltAngle: number;
  public id: number;
  public graphic: number;
  constructor(){
    this.id = 0;
    this.graphic = 0;
  }
  public update(update: Packets.PlayerUpdate){
    this.x = update.getX();
    this.y = update.getY();
    this.leftRightAngle = update.getLeftRightAngle();
    this.upDownAngle = update.getUpDownAngle();
    this.tiltAngle = update.getTiltAngle();
  }
}

export class Player extends Entity{
  public ip: string;
  public port: number;
}
