using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace VrMMOServer
{
    public class GameEntity
    {
        public Single x;
        public Single y;
        public Single leftRight;
        public Single upDown;
        public Single tilt;
        public UInt32 id;
        public UInt32 graphic;
    }
    
    class OnlinePlayerEntity : GameEntity
    {
        public IPEndPoint ip;
    }
}
