# Monogame-Spritesheet-Instancing
An efficient SpriteSheet Instancing class and shader inspired by MonoGame's SpriteBatch, supporting high-performance rendering and instancing for large-scale 2D projects.

![Main pic](images/SpriteSheet%20Instancing%20Pic.PNG)


The SpriteSheet Instancing class uses following Methods
- Begin()
  - Similar to MonoGame's SpriteBatch, this method starts collecting instances into an array.
  - Has an overload for changing the texture.
  - These instances are later sent to the graphics card using a (Vertex) Instancing Buffer for efficient rendering.

- Draw()
  - Has varios overloads
  - The position is always in the Middle of the sprite(elemnt)

- DrawTopLeft()
  - Has varios overloads
  - Draws the sprite(element) from the Left top corner
 
- End()
  - Sends the collectet instances in a (Vertex) Instancing to the graphicscard

- UpdateViewPort()
  - Updates the Viewport
  - Is nessesary for the right funktion/scale if the gamewindow size is changed
 
- Dispose()
  - Disposes the Vertex and Index buffers
  - Shoud be called if the Spritesheet instance is no longer needed

- ReturnSpritesheet()
  - Returns the Texture2D of the class
 
- ChangeSpritesheet()
  - Changes the Texture2D of the class
  - Cant be called between Begin() and End()

 - ChangeSpritesheetUnsave()
  -  Changes the Texture2D of the class
  -   Can be called between Begin() and End()

- LoadShaderAndTexture()
  - Loads the shader and Texture2D
  - Custom shaders shoud build ontop of the SpriteSheet Instancing Shader
 
- LoadShader()
  - Loads the shader
  - Custom shaders shoud build ontop of the SpriteSheet Instancing Shader
 
- 
