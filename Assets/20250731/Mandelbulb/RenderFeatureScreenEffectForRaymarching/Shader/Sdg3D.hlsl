// .x = f(p)
// .y = ∂f(p)/∂x
// .z = ∂f(p)/∂y
// .w = ∂f(p)/∂z
// .yzw = ∇f(p) with ‖∇f(p)‖ = 1

float4 sdgBox( in float3 p, in float3 b, in float r )
{
    float3  w = abs(p)-(b-r);
    float g = max(w.x,max(w.y,w.z));
    float3  q = max(w,0.0);
    float l = length(q);
    float4  f = (g>0.0)?float4(l, q/l) :
                      float4(g, w.x==g?1.0:0.0,
                              w.y==g?1.0:0.0,
                              w.z==g?1.0:0.0);
    return float4(f.x-r, f.yzw*sign(p));
}

float4 sdgTorus( in float3 p, in float ra, in float rb )
{
    float h = length(p.xz);
    return float4( length(float2(h-ra,p.y))-rb,
                 normalize(p*float3(h-ra,h,h-ra)) );
}

float4 sdgSegment( in float3 p, in float3 a, in float3 b, in float r )
{
    float3 ba = b-a;
    float3 pa = p-a;
    float h = clamp( dot(pa,ba)/dot(ba,ba), 0.0, 1.0 );
    float3  q = pa-h*ba;
    float d = length(q);
    return float4(d-r,q/d);    
}

float4 sdgEllipsoid( in float3 p, in float3 r )
{
    p /= r; float k0 =        sqrt(dot(p,p));
    p /= r; float k1 = inversesqrt(dot(p,p));
    return float4( k0*(k0-1.0)*k1, p*k1 );
}

float4 sdgSphere( in float3 p, in float r )
{
    float l = length(p);
    return float4(l-r, p/l);
}

float4 sdgLink( in float3 p, in float le, in float r1, in float r2 )
{
    float3  q = float3( p.x, p.y-clamp(p.y,-le,le), p.z );
    float w = length(q.xy);
    float l = length(float2(w-r1,q.z));
    return float4(l-r2, (q-float3(r1*q.xy/w,0.0))/l);
}

float4 sdgRoundCone( float3 p, float3 a, float3 b, float r1, float r2 )
{
    float3  ba = b - a;
    float l2 = dot(ba,ba);
    float rr = r1 - r2;
    float a2 = l2 - rr*rr;
    float il2 = 1.0/l2;
    
    float3  pa = p - a;
    float3  pb = p - b;
    float y  = dot(pa,ba);
    float z  = y-l2; //dot(pb,ba)
    float x2 = l2*dot(pa,pa)-y*y;
    float y2 = y*y;
    float z2 = z*z;
    float k  = sign(rr)*rr*rr*x2;
    if( sign(z)*a2*z2>k ) { float w=sqrt(il2*(x2+z2));
                            return float4(w-r2,pb/w); }
    if( sign(y)*a2*y2<k ) { float w=sqrt(il2*(x2+y2));
                            return float4(w-r1,pa/w); }
                          { float w=sqrt(x2*a2);
                            return float4((w+y*rr)*il2-r1,
                            il2*(rr*ba+a2*(pa*l2-y*ba)/w)); }
}

float4 sdgCappedCone( in float3 p, in float he, in float r1, in float r2 )
{
    float2  k = float2(r2-r1,2.0*he);
    float m = dot(k,k);
    float l = length(p.xz);
    float2  q = float2(r2-l, he-p.y);
    float2  a = float2(l-min(l,p.y<0.0?r1:r2), abs(p.y)-he);
    float2  b = k*clamp(dot(q,k)/m,0.0,1.0) - q;
    float s = (b.x<0.0 && a.y<0.0) ? -1.0 : 1.0;
    float la = dot(a,a);
    float lb = dot(b,b);
    return (la<lb)?float4(s*sqrt(la), 0.0, sign(p.y), 0.0 ) :
                   float4(s*sqrt(lb),float3(k.y*p.xz/l,-k.x).xzy/sqrt(m));
}

float4 sdgCylinder( in float3 p, in float he, in float r )
{
    float l = length(p.xz);
    float2  e = float2(l-r,abs(p.y)-he/2.0);
    float2  h = max(e,0.0);
    float f = length(h);
    float g = max(e.x,e.y);
    float3 du = float3(p.x/l,0.0,p.z/l);
    float3 dv = float3(0.0,p.y<0.0?-1.0:1.0,0.0);
    return (g<=0.0) ? float4( g, (e.x>e.y)?du:dv ):
                      float4( f, (h.x*du+h.y*dv)/f );
}

float4 sdgCylinder( in float3 p, in float3 a, in float3 b, in float r )
{
    float3  ba = (b-a)*0.5;
    float3  ce = (b+a)*0.5;
    float l = length(ba);
    float3  d = ba/l;

    float3  q = p-ce;
    float v = dot(q,d);
    float3  u = q-v*d;
    float k = length(u); // k = sqrt(dot(q,q)-v*v);
    float2  e = float2(k-r,abs(v)-l);
    float2  h = max(e,0.0);
    float f = length(h);
    float g = max(e.x,e.y);
    float3 du = u/k;
    float3 dv = v<0.0?-d:d;
    return (g<=0.0) ? float4(g, (e.x>e.y)?du:dv):
                      float4(f, (h.x*du+h.y*dv)/f);
}