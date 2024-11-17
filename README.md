# Monogame-Spritesheet-Instancing
An efficient SpriteSheet Instancing class and shader inspired by MonoGame's SpriteBatch, supporting high-performance rendering and instancing for large-scale 2D projects.

![Main pic](images/SpriteSheet%20Instancing%20Pic.PNG)


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
