# Monogame-Spritesheet-Instancing
An efficient SpriteSheet Instancing class and shader inspired by MonoGame's SpriteBatch, supporting high-performance rendering and instancing for large-scale 2D projects.

![Main pic](images/SpriteSheet%20Instancing%20Pic.PNG)
---
The SpriteSheet class, paired with the instancing shader, enables efficient mass instancing of 2D objects from a sprite sheet. It offers SpriteBatch-like methods for ease of use. The class supports a single texture at a time, requiring swaps for different sprite sheets. DirectX 11 (or DX10) is required.




---
## Pros
- Enables efficient mass instancing of rectangles from a single sprite sheet.
- Achieves up to 80x better CPU efficiency compared to SpriteBatch using the same sprite sheet.

## Cons
- Requires DirectX 11 (or DX10).
- Supports only one sprite sheet (texture) per draw call.
- Draw calls must be manually ordered for proper rendering (back-to-front).
- Custom shaders must be built on top of the SpriteSheet Instancing Shader.
---
![Main pic](images/SpriteSheet%20Instancing%20Pic.PNG)

<p float="left">
  <img src="images/42k Instances with SpritesheetInstancing  DepthStencilState.None about 1.2 ms.PNG" width="45%" />
  <img src="images/500 Elements with Spritebatch DepthStencilState.None about 1.2 ms.PNG="45%" />
</p>




---
## The SpriteSheet Instancing class uses following Methods
**- Begin()**
   - Similar to MonoGame's SpriteBatch, this method collects instances into an array.  
   - These instances are later sent to the graphics card using a (Vertex) Instancing Buffer for efficient rendering.
   - Includes an overload for changing the texture.

**- Draw()**
   - Offers various overloads.
   - Positions the sprite(rectangle) at its center by default.

**- DrawTopLeft()**
   - Offers various overloads.
   - Positions the sprite(rectangle) with the top-left corner as the origin.
 
**- End()**
   - Sends the collected instances to the graphics card using a (Vertex) Instancing Buffer.

**- UpdateViewPort()**
   - Updates the viewport settings.
   - Essential for maintaining accurate scaling and functionality when the game window size changes.
    
**- ReturnSpritesheet()**
   - Returns the current Texture2D associated with the class.
 
**- ChangeSpritesheet()**
   - Changes the Texture2D associated with the class.
   - Cannot be called between Begin() and End().

**- ChangeSpritesheetUnsave()**
   - Changes the Texture2D associated with the class.
   - Can be called between Begin() and End().
  
**- Dispose()**
   - Releases the Vertex and Index Buffers.
   - Should be called when the SpriteSheet instance is no longer needed.

**- LoadShaderAndTexture()**
   - Loads the shader and the Texture2D.
   - Custom shaders should build on top of the SpriteSheet Instancing Shader.
 
**- LoadShader()**
   - Loads the shader.
   - Custom shaders should build on top of the SpriteSheet Instancing Shader.
---
