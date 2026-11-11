#ifndef OBSIDIAN_AMBIENT_VISUAL_CORE
#define OBSIDIAN_AMBIENT_VISUAL_CORE
// Pipeline-independent visual math. All scene/material state enters through arguments.
float3 AmbientWakeVertex(float3 world, float2 uv, float4 motion, float2 birth,
    float wakeMode, float wakeTime, float3 rightWS, float3 upWS, out float2 wakeAge)
{
    wakeAge = 0;
    if (wakeMode > .5)
    {
        float age = clamp(wakeTime-birth.x,0,max(.001,birth.y));
        world += motion.xyz*age;
        float2 corner = uv*2-1;
        world += (rightWS*corner.x+upWS*corner.y)*motion.w;
        wakeAge = float2(age,age/max(.001,birth.y));
    }
    return world;
}
float AmbientCameraAlpha(float3 world, float3 cameraWorld, float4 cameraFade)
{
    return lerp(1,smoothstep(cameraFade.x,max(cameraFade.x+.001,cameraFade.y),
        distance(world,cameraWorld)),cameraFade.z);
}
float4 AmbientParticleColor(float2 p, float4 color, float3 world, float3 cameraWorld,
    float2 wakeAge, float form, float brightness, float4 cameraFade, float wakeMode)
{
    float radius = length(p);
    float alpha = pow(saturate(1-radius),1.6);
    if (wakeMode > .5)
        alpha = exp(-radius*radius*2.5)*(1-smoothstep(.55,1,radius))
            *smoothstep(0,.12,wakeAge.x)*(1-smoothstep(.55,1,wakeAge.y));
    else if (form > .5 && form < 1.5)
        alpha = exp(-radius*radius*7);
    else if (form < 2.5 && form > 1.5)
        alpha = .38*exp(-radius*radius*3)+.62*exp(-radius*radius*24);
    else if (form < 3.5 && form > 2.5)
        alpha = exp(-radius*radius*3.5);
    else if (form < 4.5 && form > 3.5)
        alpha = .45*exp(-radius*radius*4)+.55*exp(-radius*radius*38);
    else if (form > 4.5)
        alpha = exp(-radius*radius*2.5)*.65;
    alpha *= 1-smoothstep(.72,.98,radius);
    alpha *= AmbientCameraAlpha(world,cameraWorld,cameraFade);
    return float4(color.rgb*brightness,color.a*saturate(alpha));
}
float4 AmbientWindColor(float2 uv, float4 color, float3 world, float3 cameraWorld,
    float brightness, float4 cameraFade)
{
    color.a *= AmbientCameraAlpha(world,cameraWorld,cameraFade);
    float u = saturate(uv.x);
    float v = uv.y*2-1;
    float envelope = smoothstep(0,.09,u)*(1-smoothstep(.75,1,u));
    float edge = 1-smoothstep(.45,.98,abs(v));
    float core = exp(-v*v*14);
    return float4(color.rgb*brightness,color.a*core*.7*edge*envelope);
}
float AmbientGridLines(float2 world, float spacing, float thickness)
{
    float2 d = abs(frac(world/spacing+.5)-.5)*spacing;
    float2 aa = max(fwidth(world),.0001);
    float2 coverage = 1-smoothstep(thickness*.5-aa*.5,thickness*.5+aa*.5,d);
    coverage *= saturate(thickness/aa);
    return max(coverage.x,coverage.y);
}
float4 AmbientGridColor(float3 world, float3 cameraWorld, float4 floorColor, float4 baseColor, float4 horizonColor)
{
    float range = distance(world,cameraWorld);
    float gridCoverage = max(AmbientGridLines(world.xz,1,.025)*.3,AmbientGridLines(world.xz,5,.06)*.55);
    float3 surface = lerp(floorColor.rgb,baseColor.rgb,gridCoverage*(1-smoothstep(10,42,range)));
    return float4(lerp(surface,horizonColor.rgb,smoothstep(55,100,range)),1);
}
#endif
