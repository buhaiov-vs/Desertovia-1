using System.Collections.Concurrent;
using System.Text.Json;

namespace Desertod.Domain.World;

/// <summary>
/// Collision portion of the author's worldMap.buildLayout. The drawing generator remains unchanged.
/// Random draws are deliberately retained: changing them moves the existing ponds/ridges.
/// Browser/server parity fixtures guard this contract. Trees/bushes/reeds are walkable, as in findWay.
/// </summary>
public static class AuthoredTerrain
{
    public const int Left=-44,Top=-32,Width=120,Height=90,Right=76,Bottom=58;
    public const byte Water=2,Mountain=7,Ravine=8,Thicket=9,Rockfall=10,Marsh=11,Cave=12,Lookout=13,Trail=14;
    private static readonly ConcurrentDictionary<string,byte[]> Cache=new();
    public static byte[] For(Guid village,Wilds? wilds)
    {
        var key=village+":"+JsonSerializer.Serialize(new{wilds?.CaveX,wilds?.CaveY,wilds?.LookoutX,wilds?.LookoutY,
            wilds?.Ravine,wilds?.Passes,Obstacles=wilds?.Obstacles.Select(o=>new{o.Kind,o.X,o.Y})});
        if(Cache.Count>256)Cache.Clear();
        return Cache.GetOrAdd(key,_=>Generate(Wilderness.Seed(village.ToString()),wilds));
    }
    public static int Index(int x,int y)=>(y-Top)*Width+x-Left;
    public static bool Inside(int x,int y)=>x>=Left&&x<Right&&y>=Top&&y<Bottom;
    public static bool Blocked(byte kind)=>kind is Water or Mountain or Ravine;

    public static byte[] Generate(uint seed,Wilds? wilds)
    {
        var state=seed;
        double Next(){state=unchecked(state*1664525+1013904223);return state/4294967296d;}
        var terrain=new byte[Width*Height];var occupied=new HashSet<int>();
        bool Core(int x,int y)=>x>=0&&x<44&&y>=0&&y<26;
        bool Near(int x,int y)=>x>=-6&&y>=-6&&x<50&&y<32;
        void Mark(int x,int y,int width=1,int height=1,byte? kind=null)
        {
            for(var row=y;row<y+height;row++)for(var col=x;col<x+width;col++)
                if(Inside(col,row)){occupied.Add(Index(col,row));if(kind is {} k)terrain[Index(col,row)]=k;}
        }
        // Ground variations consume one roll per map tile before geometry generation.
        for(var i=0;i<Width*Height;i++)Next();
        var ridges=new List<(int X,int Y)>();
        void Ridge(int x,int y)
        {if(!Inside(x,y)||Near(x,y)||occupied.Contains(Index(x,y)))return;ridges.Add((x,y));Mark(x,y);}
        if(wilds is not null)
        {
            foreach(var r in wilds.Ravine){Mark(r.X,r.Top-1);Mark(r.X,r.Top,1,r.Height,Ravine);Mark(r.X,r.Top+r.Height);}
            foreach(var o in wilds.Obstacles)Mark(o.X,o.Y,1,1,o.Kind==ObstacleKind.Thicket?Thicket:Rockfall);
            var cx=wilds.CaveX;var cy=wilds.CaveY;var lx=wilds.LookoutX;var ly=wilds.LookoutY;
            Mark(cx,cy,1,1,Cave);Mark(cx-1,cy+1,3,1);Mark(lx,ly,1,1,Lookout);Mark(lx,ly+1,1,1,Trail);Mark(lx-1,ly+2,3,1);
            for(var dx=-3;dx<=3;dx++)Ridge(cx+dx,cy-1);
            for(var dx=-2;dx<=2;dx++)Ridge(cx+dx,cy-2);
            Ridge(cx-1,cy);Ridge(cx+1,cy);
            for(var dy=-2;dy<=1;dy++)for(var dx=-2;dx<=2;dx++)
            {if(Math.Abs(dx)==2&&dy!=-1&&dy!=0)continue;Ridge(lx+dx,ly+dy);}
            foreach(var pass in wilds.Passes)for(var arm=1;arm<=7;arm++)for(var side=0;side<2;side++)
                if(pass.Horizontal){Ridge(pass.X-arm,pass.Y+side);Ridge(pass.X+1+arm,pass.Y+side);}
                else{Ridge(pass.X+side,pass.Y-arm);Ridge(pass.X+side,pass.Y+1+arm);}
        }
        for(var y=Top;y<Bottom;y++)
        {
            var left=Wilderness.RiverWestBank(y,seed);Mark(left,y,4,1,Water);
            if(!Core(left,y)&&Noise(unchecked(seed+2),3,left,y)>.55&&!occupied.Contains(Index(left-1,y)))Mark(left-1,y,1,1,5);
        }
        var ponds=new List<(double X,double Y,double Rx,double Ry)>();
        for(var attempt=0;attempt<40&&ponds.Count<9;attempt++)
        {
            var x=Left+4+(int)Math.Floor(Next()*(Width-8));var y=Top+4+(int)Math.Floor(Next()*(Height-8));
            var rx=2+Next()*3.5;var ry=1.5+Next()*2.5;var fits=true;
            for(var row=(int)Math.Floor(y-ry-1);row<=y+ry+1&&fits;row++)
            for(var col=(int)Math.Floor(x-rx-1);col<=x+rx+1&&fits;col++)
                if(!Inside(col,row)||Core(col,row)||occupied.Contains(Index(col,row)))fits=false;
            if(!fits)continue;
            ponds.Add((x+.5,y+.5,rx,ry));
            for(var row=(int)Math.Floor(y-ry);row<=y+ry;row++)for(var col=(int)Math.Floor(x-rx);col<=x+rx;col++)
                if(Math.Pow((col-x)/rx,2)+Math.Pow((row-y)/ry,2)<=1)Mark(col,row,1,1,Water);
            if(Next()<.6)for(double angle=0;angle<Math.PI*2;angle+=.5)
            {
                var px=x+.5+Math.Cos(angle)*(rx+.6);var py=y+.5+Math.Sin(angle)*(ry+.6);
                if(Next()<.7)Mark((int)Math.Floor(px),(int)Math.Floor(py),1,1,5);
            }
        }
        foreach(var pond in ponds)
        {
            if(Next()>=.5)continue;var rx=pond.Rx+2.5;var ry=pond.Ry+2.5;
            for(var row=(int)Math.Floor(pond.Y-ry);row<=pond.Y+ry;row++)for(var col=(int)Math.Floor(pond.X-rx);col<=pond.X+rx;col++)
            {
                if(!Inside(col,row)||Core(col,row)||occupied.Contains(Index(col,row)))continue;
                if(Math.Pow((col+.5-pond.X)/rx,2)+Math.Pow((row+.5-pond.Y)/ry,2)>1||Noise(unchecked(seed+2),3,col,row)<.4)continue;
                Mark(col,row,1,1,Marsh);
            }
        }
        foreach(var (x,y) in new[]{(9,1),(10,1),(11,1),(9,2),(10,3),(11,2),(12,2),(8,3),(12,4)})Mark(x,y,1,1,3);
        for(var y=16;y<=20;y++)for(var x=1;x<=7;x++)if(Next()<.45)Mark(x,y,1,1,4);
        Mark(1,16,7,5);
        for(var y=14;y<=22;y++){Next();Next();Mark(28,y,2,1,5);}
        // Fixed clearings mirror the authored map. They consume no random draws.
        Mark(12,10,7,7,6);Mark(15,8,3,4,6);Mark(0,12,12,3,6);Mark(17,12,3,4,6);
        Mark(34,5,10,9,6);Mark(38,0,7,6,6);Mark(34,12,Right-34,2,6);Mark(38,15,5,4,6);Mark(39,16,3,2,3);
        void Raise(int x,int y){Mark(x,y,1,1,Mountain);Next();Next();}
        foreach(var p in ridges)Raise(p.X,p.Y);
        for(int attempt=0,made=0;attempt<40&&made<4;attempt++)
        {
            var x=Left+4+Next()*(Width-8);var y=Top+4+Next()*(Height-8);
            if(Near((int)Math.Floor(x),(int)Math.Floor(y)))continue;
            var heading=Next()*Math.PI*2;var length=10+(int)Math.Floor(Next()*20);
            var pass=length>=16?Math.Floor(length*(.3+Next()*.4)):double.NegativeInfinity;
            var ridge=new Dictionary<int,(int X,int Y)>();
            for(var step=0;step<length;step++)
            {
                heading+=(Next()-.5)*.8;x+=Math.Cos(heading);y+=Math.Sin(heading);
                if(Math.Abs(step-pass)<=2)continue;
                var radius=.9+Next()*.8;
                for(var row=(int)Math.Floor(y-radius);row<=y+radius;row++)for(var col=(int)Math.Floor(x-radius);col<=x+radius;col++)
                {
                    if(Math.Sqrt(Math.Pow(col+.5-x,2)+Math.Pow(row+.5-y,2))>radius)continue;
                    if(!Inside(col,row)||Near(col,row)||occupied.Contains(Index(col,row)))continue;
                    ridge[Index(col,row)]=(col,row);
                }
            }
            if(ridge.Count<8)continue;made++;foreach(var p in ridge.Values)Raise(p.X,p.Y);
        }
        byte[] Reach(bool climb)
        {
            var reached=new byte[terrain.Length];var queue=new Stack<int>();var start=Index(15,13);queue.Push(start);reached[start]=1;
            while(queue.TryPop(out var node))
            {
                var x=node%Width+Left;var y=node/Width+Top;
                foreach(var (dx,dy) in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                {
                    var nx=x+dx;var ny=y+dy;if(!Inside(nx,ny))continue;var n=Index(nx,ny);
                    if(reached[n]>0||!climb&&terrain[n]==Mountain)continue;
                    var deck=nx>=30&&nx<34&&ny>=12&&ny<14;
                    if(terrain[n]==Water&&!deck)continue;reached[n]=1;queue.Push(n);
                }
            }
            return reached;
        }
        var walkable=Reach(false);var climbable=Reach(true);
        for(var n=0;n<terrain.Length;n++)
            if(walkable[n]==0&&climbable[n]>0&&terrain[n] is not (Mountain or Ravine or Thicket or Rockfall or Cave or Lookout or Trail))
                Raise(n%Width+Left,n/Width+Top);
        return terrain;
    }

    private static double Noise(uint seed,double scale,double x,double y)
    {
        double Corner(int cx,int cy)
        {
            var value=seed^unchecked((uint)(cx*0x27d4eb2d))^unchecked((uint)(cy*0x165667b1));
            value=unchecked((value^(value>>16))*0x85ebca6b);value=unchecked((value^(value>>13))*0xc2b2ae35);
            return (value^(value>>16))/4294967296d;
        }
        static double Smooth(double t)=>t*t*(3-2*t);
        var fx=x/scale;var fy=y/scale;var x0=(int)Math.Floor(fx);var y0=(int)Math.Floor(fy);
        var tx=Smooth(fx-x0);var ty=Smooth(fy-y0);
        var top=Corner(x0,y0)*(1-tx)+Corner(x0+1,y0)*tx;
        var bottom=Corner(x0,y0+1)*(1-tx)+Corner(x0+1,y0+1)*tx;
        return top*(1-ty)+bottom*ty;
    }
}
