using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace VrMMOServer
{
    public class GameEntity
    {
        public const UInt32 GRAPHIC_PLAYER = 0;
        public const UInt32 GRAPHIC_SLIME = 1;

        public Single x;
        public Single y;
        public Single leftRight;
        public Single upDown;
        public Single tilt;
        public UInt32 id;
        public UInt32 graphic;
    }

    public interface AIBehavior
    {
        void doBehavior(GameWorld gw);
    }

    public class OnlinePlayerEntity : GameEntity
    {
        public IPEndPoint ip;
        public UInt32 pet_id;
        private Boolean pet_attached;
        public OnlinePlayerEntity()
        {
            this.pet_attached = false;
            this.graphic = GRAPHIC_PLAYER;
        }

        public void attachPet(UInt32 pet_id)
        {
            this.pet_id = pet_id;
            this.pet_attached = true;
        }

        public UInt32 getAttachedPet()
        {
            if (this.pet_attached)
            {
                return pet_id;
            }
            else
            {
                throw new KeyNotFoundException("No pet attached.");
            }
        }
    }

    public class OnlinePetEntity : GameEntity, AIBehavior
    {
        public const float MOVEMENT_SPEED = 0.02f;
        public const float DISTANCE_STOP_SQUARED = 4f;
        public UInt32 owner_id;
        public OnlinePetEntity(UInt32 owner)
        {
            owner_id = owner;
            this.graphic = GRAPHIC_SLIME;
        }

        public void doBehavior(GameWorld gw)
        {
            GameEntity owner = gw.getEntity(owner_id);
            float dirToOwner = GameWorld.getDirection(this.x, this.y, owner.x, owner.y);
            float distanceToOwner = GameWorld.getDistanceSquared(this.x, this.y, owner.x, owner.y);
            if (distanceToOwner > DISTANCE_STOP_SQUARED)
            {
                float newX = this.x + (float)Math.Cos(dirToOwner) * MOVEMENT_SPEED;
                float newY = this.y + (float)Math.Sin(dirToOwner) * MOVEMENT_SPEED;
                gw.moveEntity(this.id, newX, newY);
            }
        }
    }
}
