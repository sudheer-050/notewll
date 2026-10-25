using System;
using System.Drawing;
namespace Notewll
{
    // Gravity, inelastic wall/floor collisions, air drag and rolling friction in desktop pixels.
    sealed class BallPhysics
    {
        internal PointF Position, Velocity;
        internal bool Settled;
        internal float GravityMultiplier=1, Restitution=.52f, RollingFriction=1, AirDrag=.16f;
        internal void Configure(string appearance,string gravity)
        {
            var profile=BallCatalog.Profile(appearance);
            GravityMultiplier=BallCatalog.Gravity(gravity); Restitution=profile.Bounce; RollingFriction=profile.Friction;
            AirDrag=.16f*(.156f/profile.Mass)*(float)Math.Pow(profile.PhysicalDiameter/.072f,2);
        }
        internal void Reset(PointF position, PointF velocity) { Position=position; Velocity=velocity; Settled=false; }
        internal void Step(Rectangle area, Size size, float seconds, float scale)
        {
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Min(.05f,Math.Max(0,seconds))*240));
            float dt=Math.Min(.05f,Math.Max(0,seconds))/steps;
            float floor=area.Bottom-size.Height, right=area.Right-size.Width;
            for(int i=0;i<steps;i++)
            {
                bool grounded=Position.Y>=floor-.01f && Math.Abs(Velocity.Y)<1;
                if (!grounded) Velocity.Y+=1800*GravityMultiplier*scale*dt;
                Velocity.X*=(float)Math.Exp(-AirDrag*dt);
                if (grounded)
                {
                    float friction=650*RollingFriction*GravityMultiplier*scale*dt;
                    Velocity.X=Math.Sign(Velocity.X)*Math.Max(0,Math.Abs(Velocity.X)-friction);
                }
                Position.X+=Velocity.X*dt; Position.Y+=Velocity.Y*dt;
                if(Position.X<area.Left) { Position.X=area.Left; Velocity.X=Math.Abs(Velocity.X)*Restitution; }
                if(Position.X>right) { Position.X=right; Velocity.X=-Math.Abs(Velocity.X)*Restitution; }
                if(Position.Y<area.Top) { Position.Y=area.Top; Velocity.Y=Math.Abs(Velocity.Y)*Restitution; }
                if(Position.Y>=floor)
                {
                    Position.Y=floor;
                    if(Velocity.Y>0) { Velocity.Y=Velocity.Y<85*scale*(float)Math.Sqrt(GravityMultiplier) ? 0 : -Velocity.Y*Restitution; Velocity.X*=.86f; }
                }
            }
            Settled=Position.Y>=floor-.01f && Math.Abs(Velocity.Y)<1 && Math.Abs(Velocity.X)<3*scale;
            if(Settled) { Position.Y=floor; Velocity=PointF.Empty; }
        }
    }
}
