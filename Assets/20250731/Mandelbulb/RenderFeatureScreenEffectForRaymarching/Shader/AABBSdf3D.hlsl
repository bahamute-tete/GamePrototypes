struct bound3
{
    float3 mMin;
    float3 mMax;
};

bound3 aabbSegment( in float3 pa, in float3 pb, in float ra )
{
    float3 a = pb - pa;
    return bound3( min(pa, pb) - ra,
                   max(pa, pb) + ra );
}

bound3 aabbCone( in float3 pa, in float3 pb, in float ra, in float rb )
{
    float3 a = pb - pa;
    float3 e = sqrt(1.0-a*a/dot(a,a));
    float3 ea = e*ra;
    float3 eb = e*rb;
    return bound3( min(pa-ea, pb-eb),
                   max(pa+ea, pb+eb) );
}

bound3 aabbCylinder( in float3 pa, in float3 pb, in float ra )
{
    float3 a = pb - pa;
    float3 e = ra*sqrt(1.0-a*a/dot(a,a));
    return bound3( min(pa, pb)-e,
                   max(pa, pb)+e );
}

bound3 aabbDisk( in float3 ce, in float3 no, in float ra )
{
    float3 e = ra*sqrt(1.0-no*no);
    return bound3(ce-e, ce+e);
}

bound3 aabbEllipse( in float3 ce, in float3 au, in float3 av )
{
    float3 e = sqrt( au*au + av*av );
    return bound3( ce-e, ce+e );
}

bound3 aabbBezier( in float3 p0, in float3 p1, in float3 p2 )
{
    float3 a = p0-2.0*p1+p2;
    float3 b = p1-p0;
    float3 t = clamp(-b/a,0.0,1.0);
    float3 q = p0+t*(2.0*b+t*a);
    return bound3(min(min(p0,p2),q),
                  max(max(p0,p2),q));
}

bound3 aabbBezier( in float3 p0, in float3 p1, in float3 p2, in float3 p3 )
{
    float3 c  = -p0+    p1;
    float3 b  =  p0-2.0*p1+    p2;
    float3 a  = -p0+3.0*p1-3.0*p2+p3;
    float3 g  = sqrt(max(b*b-a*c,0.0));
    float3 t1 = clamp((-b-g)/a,0.0,1.0);
    float3 t2 = clamp((-b+g)/a,0.0,1.0);
    float3 q1 = p0+t1*(3.0*c+t1*(3.0*b+t1*a));
    float3 q2 = p0+t2*(3.0*c+t2*(3.0*b+t2*a));
    return bound3(min(min(p0,p3),min(q1,q2)),
                  max(max(p0,p3),max(q1,q2)));
}