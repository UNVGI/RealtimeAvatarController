#ifndef MOVIN_COMMIN_INCLUDED
#define MOVIN_COMMIN_INCLUDED

float _xmin;
float _xmax;
float _ymin;
float _ymax;
float _zmin;
float _zmax;

float4x4 tracin_camera_v;
float4x4 tracin_camera_p;
float _darkness_multiplier;

float4 get_clippos(float3 positionWS){
    return mul(tracin_camera_p, mul(tracin_camera_v, float4(positionWS, 1)));
}

float get_clippos_inside_01(float4 clippos){
    return 
        step(-clippos.w, clippos.x) 
        * step(clippos.x, clippos.w)
        * step(-clippos.w, clippos.y) 
        * step(clippos.y, clippos.w);
}

float get_ROI_xz_inside_01(float3 world){
    return step(_xmin, world.x) * step(world.x, _xmax)
    * step(-100, world.y) * step(world.y, _ymax)
    * step(_zmin, world.z) * step(world.z, _zmax);
}                   

#endif