import dgram = require('dgram');
import Packets = require('./Packets');
import Entity = require('./Entity');

var PORT = 33333;

interface GameServerInterface extends Packets.PacketListener {

}

class GameServer implements GameServerInterface {
	private server: dgram.Socket;
	private players: Entity.Player[];
	private nextPlayerId: number = 0;
	private serverTickInterval;

  constructor() {
		this.players = [];
  }

  startServer=()=>{
    this.server = dgram.createSocket('udp4');

    this.server.on('listening', ()=>{
      var address = this.server.address();
      console.log('UDP Server on ' + address.address + ":" + address.port);
    });

    this.server.on('message', this.onUDPPacket);

    this.server.bind(PORT);

		this.serverTickInterval = setInterval(this.serverTick, 30);
  }

	private timeOfLastStatusUpdate: number = 0;
	serverTick=()=>{
		var timeNow = (new Date()).getTime();
		//console.log(timeNow-this.timeOfLastStatusUpdate);
		if (timeNow-this.timeOfLastStatusUpdate>1000){
			this.timeOfLastStatusUpdate = timeNow;
			console.log("UPDATE:");
			for(var x = 0; x < this.players.length; x++){
				var player = this.players[x];
				console.log("player:"+player.ip+" x:"+player.x+" y:"+player.y);
			}
		}
		for(var x = 0; x < this.players.length; x++){
			this.sendPlayersUpdate(this.players[x]);
		}
	}

	onUDPPacket=(message, remote: dgram.RemoteInfo)=>{
      var packet = new Packets.GamePacket(message);
			packet.sortPacket(this, remote);
	}

	sendPlayersUpdate(ent: Entity.Entity) {
		var updatebuf = Packets.EntityUpdate.createPacket(ent);
		for (var x = 0; x < this.players.length; x++){
			var player = this.players[x];
			var sendbuf: Buffer;

			// Self update if sending to player's self
			if (ent == player){
				sendbuf = Packets.SelfUpdate.createPacket(ent)
			} else {
				sendbuf = updatebuf
			}

			this.server.send(sendbuf, 0, sendbuf.length, player.port, player.ip,
				function(){});
		}
	}

  onPlayerUpdate(update: Packets.PlayerUpdate, remote: dgram.RemoteInfo) {
		var player = this.findPlayerByRemoteInfo(remote);
		if (player == null){
			player = this.createPlayer(remote);
		}

		player.update(update);
  }

	createPlayer(remote: dgram.RemoteInfo): Entity.Player{
		var player = new Entity.Player();
		player.ip = remote.address;
		player.port = remote.port;
		player.graphic = 0;
		player.id = this.nextPlayerId++;
		this.players.push(player);
		return player;
	}

	findPlayerByRemoteInfo(remote: dgram.RemoteInfo): Entity.Player{
		for(var x = 0; x < this.players.length; x++){
			var player = this.players[x];
			if (player.ip == remote.address && player.port == remote.port){
				return player;
			}
		}
		return null;
	}
}

(new GameServer()).startServer();
