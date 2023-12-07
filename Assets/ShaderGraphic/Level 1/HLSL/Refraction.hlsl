

void RefractSafe_float(float3 Input, float3 Normal, float IORInput, float IORMedium, out float3 Out)
{
    // float internalIORInput = max(IORInput, 1.0);
    // float internalIORMedium = max(IORMedium, 1.0);
    // float eta = internalIORInput/internalIORMedium;
    // float cos0 = dot(Input, Normal);
    // float k = 1.0 - eta*eta*(1.0 - cos0*cos0);
    //Out = eta*Input - (eta*cos0 + sqrt(max(k, 0.0)))*Normal;
    Out = refract(Input, Normal, IORInput/IORMedium);
    
}