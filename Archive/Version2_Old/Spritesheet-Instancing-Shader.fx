//#define VS_SHADERMODEL vs_4_0 // Dx10
//#define PS_SHADERMODEL ps_4_0

#define VS_SHADERMODEL vs_5_0 // Dx11
#define PS_SHADERMODEL ps_5_0

float2 DisplaySize;       // Size of the game in Pixel x and y (Not Monitor size, Gamesize!)
float2 TextureSize;       // Spritesheet size in x and y
float4x4 View_projection; // Matrix (I Testet, Translation, Scale, RotationZ(They are working like the Spritebatch))
sampler TextureSampler : register(s0);

// Vertex shader
struct VertexInput
{        
    float4 V_Position : POSITION0;
    float4 V_uv : TEXCOORD0;
};

// Struct for the Instancing
struct InstanceInput
{
    float I_Depth : TEXCOORD1;
    float I_Rotation : TEXCOORD2;
    float4 I_Color : COLOR1;
    int I_RectangleXY : TEXCOORD3;
    int I_RectangleWH : TEXCOORD4;
    float2 I_Position : POSITION1;
    float2 I_Scale : TEXCOORD5;
};

// Pixel shader
struct PixelInput
{
    float4 P_Position : SV_Position0;
    float4 P_uv : TEXCOORD6;
    float4 P_Color : COLOR0;
};

// Rotation around the origin
// theta = Roatation in radians 2Pi (Pi = half rotation, 2Pi = full rotation)
float2x2 getRotationMatrix(float theta)
{    
    float s = sin(theta);
    float c = cos(theta);
    return float2x2(c, -s, s, c);
    // Rotate a Vector
    //
    // [ a | b ]   [ e ]   [ ae + bf ]
    // [ c | d ] * [ f ] = [ ce + df ]
    //
}

PixelInput SpriteVertexShader(VertexInput v, InstanceInput i)
{
    PixelInput output;
    
    //---Decompress Rectangle---
    // Decompress Rectangle from the Instancebuffer
    float rectangleX = i.I_RectangleXY >> 16;
    float rectangleY = i.I_RectangleXY & 65535; // 1111... 1101 & 0000... 1111 = 0000... 1101
    
    float rectangleW = i.I_RectangleWH >> 16;
    float rectangleH = i.I_RectangleWH & 65535;
    //---End Decompress Rectangle---
    
    //---Scale Rotate and place Spritesheet element around (0.0)---
    // Is the Vertex LeftUp, RightUp, LeftDown, RightDown?
    // Vertex shifting -1 = left, up | 1 = right, down
    float shiftDirectionX = sign(v.V_Position.x);
    float shiftDirectionY = sign(v.V_Position.y);
    
    // Gets the right size of the Spritesheet element
    v.V_Position.x = shiftDirectionX * rectangleW / 2 * i.I_Scale.x;
    v.V_Position.y = shiftDirectionY * rectangleH / 2 * i.I_Scale.y;
    
    // Rotate vertex around the rectangle center(origin (0.0)) with the Rotationmatrix
    float2x2 matr = getRotationMatrix(i.I_Rotation);
    v.V_Position.xy = mul(v.V_Position.xy, matr); // Rotate Vertex   
    v.V_Position.x += i.I_Position.x;
    v.V_Position.y -= i.I_Position.y; // same coordinates as Spritebatch
    // ---End Scale Rotate and place Spritesheet Element---
    
    //---About Matrix in Monogame---
    // 
    // Rotation Matrix 2x2, 4x4
    // [ cos(ß)  -sin(ß) ]
    // [ sin(ß)   cos(ß) ]
    // 
    // [ cos(ß)  -sin(ß)  0  0 ]
    // [ sin(ß)   cos(ß)  0  0 ]
    // [   0        0     1  0 ]
    // [   0        0     0  1 ]
    // 
    // Scale Matrix 4x4
    //
    // [ sX  0  0  0 ]
    // [  0 sY  0  0 ]
    // [  0  0 sZ  0 ]
    // [  0  0  0  1 ]
    //
    // Translation Matrix normal
    // 
    // [ 1  0  0  tx ] // Not used here
    // [ 0  1  0  ty ]
    // [ 0  0  1  tz ]
    // [ 0  0  0  1  ]
    //
    // Translation Matrix used here
    // 
    // [ 1   0   0   0 ]
    // [ 0   1   0   0 ]
    // [ 0   0   1   0 ]
    // [ tx  ty  tz  1 ]
    //
    // Identiity Matrix
    // 
    // [ 1  0  0  0 ]
    // [ 0  1  0  0 ]
    // [ 0  0  1  0 ]
    // [ 0  0  0  1 ]
    //
    //---End About Matrix in Monogame---
    
    
    
    // ---Scale Matrix---
    // Scales like the Monogame base Matrix in the right Displaysize
    float aspecRatio = DisplaySize.x / DisplaySize.y;    
    float scaleFactor = 2.0 / DisplaySize.y;
    float x = scaleFactor / aspecRatio;
    float y = scaleFactor;
    float4x4 scalematrix = float4x4(
    x , 0, 0, 0,
    0, y , 0, 0,
    0, 0, 1, 0,
    -1, 1, 0, 1); // -1x, 1y translates the Origin to the top Left Corner
    //---End Scale Matrix---
    
    //---Reverse Rotation--- (-r = r)
    float3x3 rotationMatrix = float3x3(View_projection[0].xyz, View_projection[1].xyz, View_projection[2].xyz);
    float3x3 invertedRotation = transpose(rotationMatrix);
    View_projection[0].xyz = invertedRotation[0];
    View_projection[1].xyz = invertedRotation[1];
    View_projection[2].xyz = invertedRotation[2];
    //---End of Reverse Rotation---    
    
    //--- Reverse y---
    View_projection._42 = -View_projection._42; // Inverts the y input to -y
    //--- End of Reverse y ---
    
    //---Multiply with the updatet (inverted) view Matrix
    float4x4 finalMatrix = mul(View_projection, scalematrix);
    v.V_Position = mul(v.V_Position, finalMatrix);
    //---End of Viewmatrix
    
    //---Depthbuffer--- (Only useful with non Transparent Sprites)
    // Adjust height for the DepthStencilState (does somthing when DepthStencilState.Default is set)
    v.V_Position.z = i.I_Depth * 0.000001; // Up to 1 Mio Elements before it reaches 1 (shoud be enough and feels fine in the test)
    //---End Depthbuffer---
    
    //---Spritesheet UV---
    // Move to the right Rectangle in the spritesheet
    v.V_uv.x += rectangleX / rectangleW;
    v.V_uv.y += rectangleY / rectangleH;
    
    // Scale correctly based on the size of the rectangle
    v.V_uv.x *= rectangleW / TextureSize.x; // 1 = 100%, 0.75 = 75%, 0.5 = 50%...
    v.V_uv.y *= rectangleH / TextureSize.y;
    //---End Spritesheet UV---
    

    //---Output Values to Pixelshader---
    output.P_Position = v.V_Position;
    output.P_uv = v.V_uv;
    output.P_Color = i.I_Color;
    //---End Output Values to Pixelshader---
    
    return output;
}


float4 SpritePixelShader(PixelInput p) : SV_TARGET
{    
    //Standard texture coloring (Color texture * Tint Color)
    float4 diffuse = tex2D(TextureSampler, p.P_uv.xy);
    return diffuse * p.P_Color;
}

technique SpriteBatch
{
    pass
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL SpritePixelShader();
    }
}
