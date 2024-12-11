using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGame.SpritesheetInstancing
{
    //Data Struct for the instancing buffer
    public struct InstanceData : IVertexType
    {
        public float Depth;         // Depth
        public float Rotation;      // Roataion                              4 byte
        public Color Color;         // Tint                                  4 byte
        public int RectangleXY;     // x,y Position in SpriteSheet           4 byte
        public int RectangleWH;     // y Height, x Width                     4 byte
        public Vector2 Position;    // Position                              8 byte
        public Vector2 Scale;       // X,Y Scale                             8 byte


        public InstanceData(float rotation, float depth, Color color, Rectangle rectangle, Vector2 pos, Vector2 scale)
        {
            Depth = depth;
            Rotation = rotation;
            Color = color;
            RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535);
            RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            Position = pos;
            Scale = scale;
        }

        public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1),  //4 byte TEXCOORD1
            new VertexElement(4, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),  //4 byte TEXCOORD2
            new VertexElement(8, VertexElementFormat.Color, VertexElementUsage.Color, 1),               //4 byte COLOR1
            new VertexElement(12, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3), //4 byte TEXCOORD3
            new VertexElement(16, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 4), //4 byte TEXCOORD4
            new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.Position, 1),         //8 byte POSITION1
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 5) //8 byte TEXCOORD5
        );

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }

    /// <summary>
    /// This class allows Instancing of rectangles out of a spritsheet or the complete texure.
    /// It draws always from back to front.
    /// Requires DX11 (DX10) and GraphicsProfile.HiDef
    /// </summary>
    public class SpritesheetInstancing
    {
        // Viewport, Buffer, Shader        
        private GraphicsDevice graphicsDevice;
        private Point viewPort;
        private Effect spritesheetInstancingShader;
        private VertexBuffer vertexBuffer;
        private IndexBuffer indexBuffer;
        private DynamicVertexBuffer dynamicinstancingBuffer;        

        // Array       
        private InstanceData[] instanceDataArray;   // Array for the Performance (List was slower by like 30%)
        private int instanceNumber;

        // Spritesheet
        private Texture2D spriteSheet;
        public bool hasSpritesheet {  get; private set; }
        private int spritesheetWidth;
        private int spritesheetHeight;

        // Matrix like spritebatch or userdefined
        private Matrix transformMatrix;
        private Matrix likeSpritebatchEmptyMatrix;

        // Throw error
        bool beginCalled;

        /// <summary>
        /// This class allows Instancing of Rectangles out of a single Spritesheet.
        /// The drawing is performed from back to front.
        /// <para>
        /// <b>Important:</b> This class requires DirectX 11 (or DirectX 10) and the <see cref="GraphicsProfile.HiDef"/> graphics profile.
        /// </para>
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="viewPortSizeXY"></param>
        /// <param name="spritesheetInstancingShader"></param>
        /// <param name="spriteSheet"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SpritesheetInstancing(GraphicsDevice graphicsDevice, Point viewPortSizeXY, Effect spritesheetInstancingShader, Texture2D spriteSheet = null)
        {
            // Like Spritebatch
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice", "The GraphicsDevice must not be null when creating new resources.");
            }
            if (spritesheetInstancingShader == null)
            {
                throw new ArgumentNullException("spritesheetInstancingShader", "The Spritesheet Instancing Shader cant be null");
            }

            this.graphicsDevice = graphicsDevice;
            this.viewPort = viewPortSizeXY;

            ChangeSpritesheet(spriteSheet);
            LoadShader(spritesheetInstancingShader);
            CreateBaseVertexAndIndexBuffer();

            CreateStandardMatrix();                       
            
            // Create Array with a Space for 1 Element
            instanceDataArray = new InstanceData[1];
        }

        /// <summary>
        /// Creates all the required Buffers
        /// </summary>
        private void CreateBaseVertexAndIndexBuffer()
        {
            // The Base Vertex Positions
            VertexPositionTexture[] vertices = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1f, -1f, 0), new Vector2(0, 1)), // Down Left
                new VertexPositionTexture(new Vector3(-1f, 1f, 0), new Vector2(0, 0)),  // Top Left
                new VertexPositionTexture(new Vector3(1f, -1f, 0), new Vector2(1, 1)),  // Down Right
                new VertexPositionTexture(new Vector3(1f, 1f, 0), new Vector2(1, 0))    // Top Right
            };

            // VertexBuffer for the Single Quad
            vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            // Creates the Index-Array for 2 triangles (one Quad together)
            short[] indices = new short[] { 0, 1, 2, 2, 1, 3 };

            // Indexbuffer for the 2 Triangles
            indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            // Dynamicinstancing Buffer
            dynamicinstancingBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(InstanceData), 1, BufferUsage.WriteOnly);
        }

        /// <summary>
        /// This Matrix moves the Orign(0,0) point to the left Top Corner
        /// </summary>
        private void CreateStandardMatrix()
        {
            likeSpritebatchEmptyMatrix = Matrix.Identity;
        }

        /// <summary>
        /// Loads a new Shader
        /// </summary>
        /// <param name="spritesheetInstancingShader"></param>
        public void LoadShader(Effect spritesheetInstancingShader)
        {
            if (spritesheetInstancingShader == null)
            {
                throw new ArgumentNullException("spritesheetInstancingShader", "The Spritesheet Instancing Shader cant be null");
            }
            this.spritesheetInstancingShader = spritesheetInstancingShader;
        }

        /// <summary>
        /// Loads a new Shader
        /// Changes the Spritesheet with a new one
        /// </summary>
        /// <param name="spritesheetInstancingShader"></param>
        /// <param name="spriteSheet"></param>
        public void LoadShaderAndTexture(Effect spritesheetInstancingShader, Texture2D spriteSheet)
        {
            if (spritesheetInstancingShader == null)
            {
                throw new ArgumentNullException("spritesheetInstancingShader", "The Spritesheet Instancing Shader cant be null");
            }

            this.spritesheetInstancingShader = spritesheetInstancingShader;
            this.spriteSheet = spriteSheet;
            if (spriteSheet != null)
            {
                spritesheetWidth = spriteSheet.Width;
                spritesheetHeight = spriteSheet.Height;
                hasSpritesheet = true;
            }
            else
            {
                hasSpritesheet = false;
            }
        }

        /// <summary>
        /// Changes the Spritesheet with a new one
        /// Not in a Drawcall
        /// </summary>
        /// <param name="spriteSheet"></param>
        public void ChangeSpritesheet(Texture2D spriteSheet)
        {
            if (beginCalled)
            {
                throw new InvalidOperationException("Spritesheet swap mid draw is not recomended. Use ChangeSpritesheetUnsave()");
            }

            this.spriteSheet = spriteSheet;
            if (spriteSheet != null)
            {
                spritesheetWidth = spriteSheet.Width;
                spritesheetHeight = spriteSheet.Height;
                hasSpritesheet = true;
            }
            else
            {
                hasSpritesheet = false;
            }
        }

        /// <summary>
        /// Changes the Spritesheet with a new one
        /// In a Drawcall
        /// </summary>
        /// <param name="spriteSheet"></param>
        public void ChangeSpritesheetUnsave(Texture2D spriteSheet)
        {
            this.spriteSheet = spriteSheet;
            if (spriteSheet != null)
            {
                spritesheetWidth = spriteSheet.Width;
                spritesheetHeight = spriteSheet.Height;
                hasSpritesheet = true;
            }
            else
            {
                hasSpritesheet = false;
            }
        }

        /// <summary>
        /// Returns the current Texture2D sprite(sheet)
        /// </summary>
        /// <returns></returns>
        public Texture2D ReturnSpritesheet()            
        { 
            return spriteSheet; 
        }

        /// <summary>
        /// Updates the Viewport
        /// <para>
        /// <b>Important:</b> This needs to be called if the resolution changes
        /// </para>
        /// </summary>
        /// <param name="viewPort">Size of the game in Pixel</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void UpdateViewPort(Point viewPort)
        {
            if (viewPort.X <= 0 || viewPort.Y <= 0)
            {
                throw new InvalidOperationException("Display size cant be zero or smaller");
            }
            this.viewPort.X = viewPort.X;
            this.viewPort.Y = viewPort.Y;
        }

        /// <summary>
        /// Disposes the vertex and index buffers to free up GPU resources.
        /// This shoud be called when this instance is no longer needed.
        /// </summary>
        public void Dispose()
        {
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            dynamicinstancingBuffer?.Dispose();

            vertexBuffer = null;
            indexBuffer = null;
            dynamicinstancingBuffer = null;
            instanceDataArray = null;
        }

        /// <summary>
        /// Starts collecting the “drawcalls” in an array before sending them to the graphics card in a (Vetex)Instancing buffer.
        /// <paramref  name="numberOfElements"/> sets the array capacity (standart = 1).
        /// The array will automatically grow as needed.
        /// </summary>
        /// <param name="blendState">AlphaBlend if empty</param>        
        /// <param name="transforMatrix">0.0 is in the top left corner if empty</param>        
        /// <param name="numberOfElements">Sizes the Array to the numberOfElements</param>
        public void Begin(Matrix? transforMatrix = null, BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, int numberOfElements = 0)
        {
            // Like Spritebatch
            if (beginCalled)
            {
                throw new InvalidOperationException("Begin cannot be called again until End has been successfully called.");
            }
            // For the End Method
            beginCalled = true;
            // Return if there is no Texture
            if (!hasSpritesheet)
            {                
                return;
            }

            graphicsDevice.BlendState = blendState ?? BlendState.AlphaBlend; // Standard is AlphaBlend            
            graphicsDevice.DepthStencilState = depthStencilState ?? DepthStencilState.None;
            graphicsDevice.SamplerStates[0] = samplerState ?? SamplerState.LinearClamp;
            graphicsDevice.RasterizerState = rasterizerState ?? RasterizerState.CullNone;
            transformMatrix = transforMatrix ?? likeSpritebatchEmptyMatrix; // Like the Spritebatch

            // Uses an Array if the number of elements are known
            // If size of the Array is equal to the numbersOfElements the array keeps its size, no new allocation on the heap
            if (numberOfElements > 0)
            {
                // Array size increase if number of Elements are bigger
                if (instanceDataArray.Length < numberOfElements)
                {
                    instanceDataArray = new InstanceData[numberOfElements];
                }
                instanceNumber = 0;
            }
            // Resize the Array (Capacity not known)            
            else
            {
                instanceDataArray = new InstanceData[1];
                instanceNumber = 0;
            }            
        }

        /// <summary>
        /// Starts collecting the “drawcalls” in an array before sending them to the graphics card in a (Vetex)Instancing buffer.
        /// <paramref  name="numberOfElements"/> sets the array capacity (standart = 1).
        /// The array will automatically grow/shrink as needed.
        /// Changes the Texture(Spritesheet) before the drawcall
        /// </summary>
        /// <param name="blendState">AlphaBlend if empty</param>        
        /// <param name="transforMatrix">0.0 is in the top left corner if empty</param>        
        /// <param name="numberOfElements">Sizes the Array to the numberOfElements</param>
        public void Begin(Texture2D spriteSheet, Matrix? transforMatrix = null, BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, int numberOfElements = 0)
        {
            // Like Spritebatch
            if (beginCalled)
            {
                throw new InvalidOperationException("Begin cannot be called again until End has been successfully called.");
            }

            // Changes the Spritesheet
            ChangeSpritesheet(spriteSheet);

            // For the End Method
            beginCalled = true;
            // Return if there is no Texture
            if (!hasSpritesheet)
            {
                return;
            }

            graphicsDevice.BlendState = blendState ?? BlendState.AlphaBlend; // Standard is AlphaBlend            
            graphicsDevice.DepthStencilState = depthStencilState ?? DepthStencilState.None;
            graphicsDevice.SamplerStates[0] = samplerState ?? SamplerState.LinearClamp;
            graphicsDevice.RasterizerState = rasterizerState ?? RasterizerState.CullNone;
            transformMatrix = transforMatrix ?? likeSpritebatchEmptyMatrix; // Like the Spritebatch

            // Uses an Array if the number of elements are known
            // If size of the Array is equal to the numbersOfElements the array keeps its size, no new allocation on the heap
            if (numberOfElements > 0)
            {
                // Array size increase only if number of elements are bigger than the Array
                if (instanceDataArray.Length < numberOfElements)
                {
                    //instanceDataArray = null;
                    instanceDataArray = new InstanceData[numberOfElements];
                }
                instanceNumber = 0;
            }
            // Resize the Array (Capacity not known)            
            else
            {
                instanceDataArray = new InstanceData[1];
                instanceNumber = 0;
            }
        }

        /// <summary>
        /// Resizes the Array with 2x the Capacity
        /// </summary>
        private void ResizeTheInstancesArray()
        {
            Array.Resize(ref instanceDataArray, instanceDataArray.Length * 2);
        }


        /// <summary>
        /// Adds the complete sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and drawn at position (0,0).
        /// </para>
        /// </summary>        
        public void DrawTopLeft()
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = 0;
            instanceDataArray[instanceNumber].RectangleWH = (spritesheetWidth << 16) | (spritesheetHeight & 65535);
            instanceDataArray[instanceNumber].Position = new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Adds the complete sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>
        /// <param name="position">The top left corner position of the sprite.</param>
        public void DrawTopLeft(Vector2 position)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = 0;
            instanceDataArray[instanceNumber].RectangleWH = (spritesheetWidth << 16) | (spritesheetHeight & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Adds the complete sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="position">The top left corner position of the sprite.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Vector2 position, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = 0;
            instanceDataArray[instanceNumber].RectangleWH = (spritesheetWidth << 16) | (spritesheetHeight & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>      
        /// <param name="position">The top left corner position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>   
        /// <param name="position">The top left corner position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>
        /// <param name="position">The top left corner position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Vector2 scale)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Vector2 scale, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }

        // Normal Draw
        
        /// <summary>
        /// Adds the complete sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and drawn at position (0,0).
        /// </para>
        /// </summary>    
        public void Draw()
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = 0;
            instanceDataArray[instanceNumber].RectangleWH = (spritesheetWidth << 16) | (spritesheetHeight & 65535);
            instanceDataArray[instanceNumber].Position = new Vector2(0);
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }


        /// <summary>
        /// Adds the complete sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided position.
        /// </para>
        /// </summary>
        /// <param name="position">The position of the sprite.</param>
        public void Draw(Vector2 position)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = 0;
            instanceDataArray[instanceNumber].RectangleWH = (spritesheetWidth << 16) | (spritesheetHeight & 65535);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>        
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        public void Draw(Vector2 position, Rectangle rectangle)
        {   
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>        
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Vector2 position, Rectangle rectangle, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite element in radians.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Vector2 position, Rectangle rectangle, float rotation, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = rotation;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }


        /// <summary>
        /// Adds a sprite or spritesheet element to the draw array for rendering.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite element in radians.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Vector2 position, Rectangle rectangle, float rotation, Vector2 scale, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = rotation;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataArray[instanceNumber].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }

        /// <summary>
        /// Adds the created Array from the draws methods together in a dynamic vertexbuffer(Instancingbuffer) for the single drawcall.
        /// <para>
        /// The textures are drawn in the order in which the <c>Draw()/DrawTopLeft()</c> methods were called.
        /// </para>
        /// </summary>
        public void End()
        {
            // Like Spritebatch
            if (!beginCalled)
            {
                throw new InvalidOperationException("Begin must be called before calling End.");
            }
            beginCalled = false;
            // Return if there is no Texture
            if (!hasSpritesheet)
            {
                return;
            }
            // Are there more instances than 0?
            if (instanceNumber < 1)
            {
                return;
            }            
            // Without the Array there is no Draw call
            if (instanceDataArray == null)
            {
                return;
            }

            // Sets the Instancingbuffer
            // Dispose the buffer from the last Frame if the (vetex)instancingbuffer has changed
            if (dynamicinstancingBuffer.VertexCount < instanceNumber)
            {
                dynamicinstancingBuffer?.Dispose();
                dynamicinstancingBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(InstanceData), instanceNumber, BufferUsage.WriteOnly);
            }

            // Fills the (vertex)instancingbuffer
            dynamicinstancingBuffer.SetData(instanceDataArray, 0, instanceNumber, SetDataOptions.Discard);                

            // Binds the vertexBuffers
            graphicsDevice.SetVertexBuffers(new VertexBufferBinding[]
            {
                new VertexBufferBinding(vertexBuffer, 0, 0), // Dreieck-Versetzungen
                new VertexBufferBinding(dynamicinstancingBuffer, 0, 1) // Instanzdaten
            });

            // Indexbuffer
            graphicsDevice.Indices = indexBuffer;
            

            // Paramethers to the shader
            // Viewport
            // TextureSize
            // Viewmatrix
            // SpriteSheet Texture2D            
            spritesheetInstancingShader.Parameters["DisplaySize"].SetValue(new Vector2(viewPort.X, viewPort.Y));
            spritesheetInstancingShader.Parameters["TextureSize"].SetValue(new Vector2(spritesheetWidth, spritesheetHeight));
            spritesheetInstancingShader.Parameters["View_projection"].SetValue(transformMatrix);
            spritesheetInstancingShader.Parameters["TextureSampler"].SetValue(spriteSheet);

            // Activates the shader
            spritesheetInstancingShader.CurrentTechnique.Passes[0].Apply();                       

            // Draws the 2 triangles on the screen
            graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, instanceNumber);
        }
    }

    public class SpritesheetInstancingAdv
    {
        // Viewport, Buffer, Shader        
        private GraphicsDevice graphicsDevice;
        private Point viewPort;
        private Effect spritesheetInstancingShader;
        private VertexBuffer vertexBuffer;
        private IndexBuffer indexBuffer;
        private DynamicVertexBuffer dynamicinstancingBuffer;

        // Array       
        private InstanceData[][] instanceDataJaggedArray;   // Jagged Array for the Performance (List was slower by like 30%)
        private int[] instanceNumbers;

        // Spritesheets
        private Texture2D[] spriteSheets; // To do array an texturen
        public bool hasSpritesheets { get; private set; }
        private Point[] spritesheetWidthAndHeight;

        // Matrix like spritebatch or userdefined
        private Matrix transformMatrix;
        private Matrix likeSpritebatchEmptyMatrix;

        // Throw error
        bool beginCalled;

        /// <summary>
        /// This class allows the Instancing of Rectangles out of mutiple Spritesheets.
        /// <para>        
        /// Textures are drawn in the order they appear in the internal <c>spriteSheets</c> array, starting with <c>spriteSheets[0]</c>, followed by <c>spriteSheets[1]</c>, and so on.
        /// The drawing is performed from back to front.
        /// </para>
        /// <b>Important:</b> This class requires DirectX 11 (or DirectX 10) and the <see cref="GraphicsProfile.HiDef"/> graphics profile.
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="viewPortSizeXY"></param>
        /// <param name="spritesheetInstancingShader"></param>
        /// <param name="spriteSheets"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SpritesheetInstancingAdv(GraphicsDevice graphicsDevice, Point viewPortSizeXY, Effect spritesheetInstancingShader, Texture2D[] spriteSheets = null)
        {
            // Like Spritebatch
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice", "The GraphicsDevice must not be null when creating new resources.");
            }
            if (spritesheetInstancingShader == null)
            {
                throw new ArgumentNullException("spritesheetInstancingShader", "The Spritesheet Instancing Shader cant be null");
            }

            this.graphicsDevice = graphicsDevice;
            this.viewPort = viewPortSizeXY;

            ChangeSpritesheets(spriteSheets);
            LoadShader(spritesheetInstancingShader);
            CreateBaseVertexAndIndexBuffer();

            CreateStandardMatrix();
        }

        /// <summary>
        /// Creates all the required Buffers
        /// </summary>
        private void CreateBaseVertexAndIndexBuffer()
        {
            // The Base Vertex Positions
            VertexPositionTexture[] vertices = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1f, -1f, 0), new Vector2(0, 1)), // Down Left
                new VertexPositionTexture(new Vector3(-1f, 1f, 0), new Vector2(0, 0)),  // Top Left
                new VertexPositionTexture(new Vector3(1f, -1f, 0), new Vector2(1, 1)),  // Down Right
                new VertexPositionTexture(new Vector3(1f, 1f, 0), new Vector2(1, 0))    // Top Right
            };

            // VertexBuffer for the Single Quad
            vertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertices.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(vertices);

            // Creates the Index-Array for 2 triangles (one Quad together)
            short[] indices = new short[] { 0, 1, 2, 2, 1, 3 };

            // Indexbuffer for the 2 Triangles
            indexBuffer = new IndexBuffer(graphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);

            // Dynamicinstancing Buffer
            dynamicinstancingBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(InstanceData), 1, BufferUsage.WriteOnly);
        }

        /// <summary>
        /// This Matrix moves the Orign(0,0) point to the left Top Corner
        /// </summary>
        private void CreateStandardMatrix()
        {
            likeSpritebatchEmptyMatrix = Matrix.Identity;
        }

        /// <summary>
        /// Loads a new Shader
        /// </summary>
        /// <param name="spritesheetInstancingShader"></param>
        public void LoadShader(Effect spritesheetInstancingShader)
        {
            if (spritesheetInstancingShader == null)
            {
                throw new ArgumentNullException("spritesheetInstancingShader", "The Spritesheet Instancing Shader cant be null");
            }
            this.spritesheetInstancingShader = spritesheetInstancingShader;
        }

        /// <summary>
        /// Replaces the current spritesheets with a new set of sprites.
        /// This cant be performed during a draw call.
        /// </summary>
        /// <param name="spriteSheets"></param>
        public void ChangeSpritesheets(Texture2D[] spriteSheets)
        {
            if (beginCalled)
            {
                throw new InvalidOperationException("Spritesheet(s) swap mid draw is not possible.");
            }

            this.spriteSheets = spriteSheets;
            if (spriteSheets != null)
            {
                // Erschaffe die Arrays für die Anzahl der Elemente für jedes Spritesheets
                instanceNumbers = new int[spriteSheets.Length];

                // Hilfs Arrays Größe anpassen
                spritesheetWidthAndHeight = new Point[spriteSheets.Length];

                // Befülle die Hilfs Arrays für die Größe der Spritesheets
                for (int i = 0; i < spriteSheets.Length; i++)
                {
                    if (spriteSheets[i] != null)
                    {
                        spritesheetWidthAndHeight[i].X = spriteSheets[i].Width;
                        spritesheetWidthAndHeight[i].Y = spriteSheets[i].Height;
                    }
                }

                // Jagged Array erstellen
                instanceDataJaggedArray = new InstanceData[spriteSheets.Length][];
                for (int i = 0; i < spriteSheets.Length; i++)
                {
                    // Elemente des Jagged Arrays erstellen
                    instanceDataJaggedArray[i] = new InstanceData[1]; 
                }

                hasSpritesheets = true;
            }
            else
            {
                hasSpritesheets = false;
            }
        }

        /// <summary>
        /// Returns the current Texture2D array (sprite(sheet)s).
        /// </summary>
        /// <returns></returns>
        public Texture2D[] ReturnSpritesheets()
        {
            return spriteSheets;
        }

        /// <summary>
        /// Updates the Viewport
        /// <para>
        /// <b>Important:</b> This needs to be called if the resolution changes
        /// </para>
        /// </summary>
        /// <param name="viewPort">Size of the game in Pixel</param>
        /// <exception cref="InvalidOperationException"></exception>
        public void UpdateViewPort(Point viewPort)
        {
            if (viewPort.X <= 0 || viewPort.Y <= 0)
            {
                throw new InvalidOperationException("Display size cant be zero or smaller");
            }
            this.viewPort.X = viewPort.X;
            this.viewPort.Y = viewPort.Y;
        }

        /// <summary>
        /// Disposes the vertex and index buffers to free up GPU resources.
        /// This shoud be called when this instance is no longer needed.
        /// </summary>
        public void Dispose()
        {
            vertexBuffer?.Dispose();
            indexBuffer?.Dispose();
            dynamicinstancingBuffer?.Dispose();

            vertexBuffer = null;
            indexBuffer = null;
            dynamicinstancingBuffer = null;
            instanceDataJaggedArray = null;
            spriteSheets = null;
        }


        /// <summary>
        /// Resizes the Arrays of the Jagged Array with 2x the Capacity
        /// </summary>
        private void ResizeTheInstancesArray(int jaggedArrayNumber)
        {
            Array.Resize(ref instanceDataJaggedArray[jaggedArrayNumber], instanceDataJaggedArray[jaggedArrayNumber].Length * 2);
        }

        /// <summary>
        /// Starts collecting draw calls in jagged arrays, which will be sent to the graphics card via a (Vertex) Instancing buffer, one after another.
        /// The <paramref name="numberOfElementsArray"/> parameter defines the initial capacity for each texture in the internal <c>spriteSheets</c> array (default = 1).
        /// The jagged arrays will automatically resize as needed.
        /// <para>
        /// <b>Important:</b> <paramref name="numberOfElementsArray"/> is defining the expected number of elements per texture in the internal <c>spriteSheets</c> array. 
        /// This should be set manually to prevent constant resizing of the internal jagged arrays (can be set higher than needed).
        /// </para>
        /// </summary>
        /// <param name="blendState">AlphaBlend if empty</param>        
        /// <param name="transforMatrix">0.0 is in the top left corner if empty</param>        
        /// <param name="numberOfElementsArray">An array defining the expected number of elements per texture in the internal <c>spriteSheets</c> array. 
        /// This should be set manually to prevent constant resizing of the internal jagged arrays (can be set higher than needed).</param>
        public void Begin(Matrix? transforMatrix = null, BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, int[] numberOfElementsArray = null)
        {
            // Like Spritebatch
            if (beginCalled)
            {
                throw new InvalidOperationException("Begin cannot be called again until End has been successfully called.");
            }
            // For the End Method
            beginCalled = true;
            // Return if there are no Textures
            if (!hasSpritesheets)
            {
                return;
            }

            graphicsDevice.BlendState = blendState ?? BlendState.AlphaBlend; // Standard is AlphaBlend            
            graphicsDevice.DepthStencilState = depthStencilState ?? DepthStencilState.None;
            graphicsDevice.SamplerStates[0] = samplerState ?? SamplerState.LinearClamp;
            graphicsDevice.RasterizerState = rasterizerState ?? RasterizerState.CullNone;
            transformMatrix = transforMatrix ?? likeSpritebatchEmptyMatrix; // Like the Spritebatch

            // If the numberOfElements is not given or wrong
            if (numberOfElementsArray == null || numberOfElementsArray.Length != spriteSheets.Length)
            {
                // Create new Arrays
                numberOfElementsArray = new int[spriteSheets.Length];
                instanceNumbers = new int[spriteSheets.Length];
            }

            // Scale the instanceNumbers right
            if (instanceNumbers.Length != numberOfElementsArray.Length)
            {
                instanceNumbers = new int[numberOfElementsArray.Length];
            }

            // For each SpriteSheet in spriteSheets
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                // No spriteSheet continue
                if (spriteSheets[i] == null)
                {
                    continue;
                }

                // Uses an Jagged Array if the number of elements are known
                // If size of the Jagged Array is equal to the numbersOfElements the array keeps its size, no new allocation on the heap
                if (numberOfElementsArray[i] > 0)
                {
                    // If jagged Array is smaller than the numberOfElements, new numberOfElements
                    if (instanceDataJaggedArray[i].Length < numberOfElementsArray[i])
                    {
                        instanceDataJaggedArray[i] = new InstanceData[numberOfElementsArray[i]];
                    }
                    instanceNumbers[i] = 0;
                }
                // Resize the Jagged Array (Capacity not known) 
                else
                {
                    instanceDataJaggedArray[i] = new InstanceData[1];
                    instanceNumbers[i] = 0;
                }
            }
        }

        // Draw Top Left Unsave

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para>
        /// The sprite is centered at its top left point and drawn at position (0,0).
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        public void DrawTopLeftUnsave(int textureIndex)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>    
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para> 
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>    
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para> 
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>        
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position, Rectangle rectangle)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para> 
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para> 
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position, Rectangle rectangle, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para> 
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para>
        /// <para> 
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position, Rectangle rectangle, Vector2 scale)
        {            
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para> 
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// </para> 
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeftUnsave(int textureIndex, Vector2 position, Rectangle rectangle, Vector2 scale, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }
        
        // Draw Top Left

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and drawn at position (0,0).
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>            
        public void DrawTopLeft(Texture2D texture)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>    
        public void DrawTopLeft(Texture2D texture, Vector2 position)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>    
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Texture2D texture, Vector2 position, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(spritesheetWidthAndHeight[textureIndex].X / 2, spritesheetWidthAndHeight[textureIndex].Y / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>        
        public void DrawTopLeft(Texture2D texture, Vector2 position, Rectangle rectangle)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Texture2D texture, Vector2 position, Rectangle rectangle, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        public void DrawTopLeft(Texture2D texture, Vector2 position, Rectangle rectangle, Vector2 scale)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its top left point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawTopLeft(Texture2D texture, Vector2 position, Rectangle rectangle, Vector2 scale, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }        

        // Unsave Draw

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        public void DrawUnsave(int textureIndex)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = new Vector2(0);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite in world space.</param>
        public void DrawUnsave(int textureIndex, Vector2 position)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite in world space.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        public void DrawUnsave(int textureIndex, Vector2 position, Rectangle rectangle)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite in world space.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawUnsave(int textureIndex, Vector2 position, Rectangle rectangle, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite in world space.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite in radians.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawUnsave(int textureIndex, Vector2 position, Rectangle rectangle, float rotation, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = rotation;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// This method assumes the <paramref name="textureIndex"/> is valid and corresponds to an existing sprite in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Adds a sprite or spritesheet element to the draw list for rendering without validating the input.
        /// </para>
        /// <para>
        /// Centers the sprite by its middle point and prepares it for rendering.
        /// </para>
        /// </summary>  
        /// <param name="textureIndex">
        /// The index of the texture (sprite or spritesheet) in the internal <c>spriteSheets</c> array.
        /// Must be a valid index. No check is performed in this method.
        /// </param>
        /// <param name="position">The position of the sprite in world space.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite in radians.</param>
        /// <param name="scale">The scale of the sprite.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void DrawUnsave(int textureIndex, Vector2 position, Rectangle rectangle, float rotation, Vector2 scale, Color color)
        {
            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = rotation;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }

        // Normal Draw

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        public void Draw(Texture2D texture)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = new Vector2(0);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        public void Draw(Texture2D texture, Vector2 position)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = 0;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (spritesheetWidthAndHeight[textureIndex].X << 16) | (spritesheetWidthAndHeight[textureIndex].Y & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        public void Draw(Texture2D texture, Vector2 position, Rectangle rectangle)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = Color.White;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Texture2D texture, Vector2 position, Rectangle rectangle, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = 0f;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite element in radians.</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Texture2D texture, Vector2 position, Rectangle rectangle, float rotation, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = rotation;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = Vector2.One;

            instanceNumbers[textureIndex]++;
        }

        /// <summary>
        /// Adds a sprite or spritesheet element to the draw list for rendering, ensuring the input texture matches an element in the internal <c>spriteSheets</c> array.
        /// <para>
        /// The sprite is centered at its middle point and transformed based on the provided parameters.
        /// </para>
        /// </summary>  
        /// <param name="texture">The texture to render, which must match one of the elements in the internal <c>spriteSheets</c> array.</param>
        /// <param name="position">The position of the sprite.</param>
        /// <param name="rectangle">The source rectangle from the spritesheet.</param>
        /// <param name="rotation">The rotation of the sprite element in radians.</param>
        /// <param name="scale">The scale of the sprite. (x, y)</param>
        /// <param name="color">The color tint applied to the sprite.</param>
        public void Draw(Texture2D texture, Vector2 position, Rectangle rectangle, float rotation, Vector2 scale, Color color)
        {
            // return if there is no spritesheet
            int textureIndex = -1;
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                if (texture == spriteSheets[i])
                {
                    textureIndex = i;
                }
            }
            if (textureIndex == -1)
            {
                return;
            }

            // Double the jagged array size
            if (instanceNumbers[textureIndex] >= instanceDataJaggedArray[textureIndex].Length)
            {
                ResizeTheInstancesArray(textureIndex);
            }

            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Depth = instanceNumbers[textureIndex];
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Rotation = rotation;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Color = color;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleXY = (rectangle.X << 16) | (rectangle.Y & 65535); // Moving the right part of x (16bits) to the left and add the right part of y (16bits) with a mask (65535) together
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].RectangleWH = (rectangle.Width << 16) | (rectangle.Height & 65535);
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Position = position;
            instanceDataJaggedArray[textureIndex][instanceNumbers[textureIndex]].Scale = scale;

            instanceNumbers[textureIndex]++;
        }


        /// <summary> 
        /// Prepares a single draw call for each texture in the internal <c>spriteSheets</c> array.
        /// <para>
        /// Textures are drawn in the order they appear in the internal <c>spriteSheets</c> array, starting with <c>spriteSheets[0]</c>, followed by <c>spriteSheets[1]</c>, and so on.
        /// </para>
        /// </summary>
        public void End()
        {
            // Like Spritebatch
            if (!beginCalled)
            {
                throw new InvalidOperationException("Begin must be called before calling End.");
            }
            beginCalled = false;
            // Return if there is no Textures
            if (!hasSpritesheets)
            {
                return;
            }

            // For each Spritesheet
            for (int i = 0; i < spriteSheets.Length; i++)
            {
                // Are there more instances than 0?
                if (instanceNumbers[i] < 1)
                {
                    continue;
                }

                // Continue if there is no Spritesheet
                if (spriteSheets[i] == null)
                {
                    continue;
                }

                // Sets the Instancingbuffer
                // Dispose the buffer from the last Frame if the (vetex)instancingbuffer has changed
                if (dynamicinstancingBuffer.VertexCount < instanceNumbers[i])
                {
                    dynamicinstancingBuffer?.Dispose();
                    dynamicinstancingBuffer = new DynamicVertexBuffer(graphicsDevice, typeof(InstanceData), instanceNumbers[i], BufferUsage.WriteOnly);
                }

                // Fills the (vertex)instancingbuffer
                dynamicinstancingBuffer.SetData(instanceDataJaggedArray[i], 0, instanceNumbers[i], SetDataOptions.Discard);

                // Binds the vertexBuffers
                graphicsDevice.SetVertexBuffers(new VertexBufferBinding[]
                {
                    new VertexBufferBinding(vertexBuffer, 0, 0), // Dreieck-Versetzungen
                    new VertexBufferBinding(dynamicinstancingBuffer, 0, 1) // Instanzdaten
                });

                // Indexbuffer
                graphicsDevice.Indices = indexBuffer;


                // Paramethers to the shader
                // Viewport
                // TextureSize
                // Viewmatrix
                // SpriteSheet Texture2D            
                spritesheetInstancingShader.Parameters["DisplaySize"].SetValue(new Vector2(viewPort.X, viewPort.Y));
                spritesheetInstancingShader.Parameters["TextureSize"].SetValue(new Vector2(spritesheetWidthAndHeight[i].X, spritesheetWidthAndHeight[i].Y));
                spritesheetInstancingShader.Parameters["View_projection"].SetValue(transformMatrix);
                spritesheetInstancingShader.Parameters["TextureSampler"].SetValue(spriteSheets[i]);

                // Activates the shader
                spritesheetInstancingShader.CurrentTechnique.Passes[0].Apply();

                // Draws the 2 triangles on the screen
                graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 2, instanceNumbers[i]);
            }
        }
    }
}
