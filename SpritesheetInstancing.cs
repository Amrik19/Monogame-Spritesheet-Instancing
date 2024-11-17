using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace Monogame.SpritesheetInstancing
{
    /// <summary>
    /// This class allows Instancing of rectangles out of a spritesheet or the complete texure.
    /// It draws always from back to front.
    /// Requires DX11 (DX10) and GraphicsProfile.HiDef
    /// </summary>
    public class SpritesheetInstancing
    {
        // Viewport, Buffer, Shader
        private RasterizerState rasterizer;
        private SamplerState samplerState;
        private GraphicsDevice graphicsDevice;
        private Point viewPort;
        private Effect spritesheetInstancingShader;
        private VertexBuffer vertexBuffer;
        private IndexBuffer indexBuffer;
        private DynamicVertexBuffer dynamicinstancingBuffer;
        private bool emptyBuffers;

        // Array       
        private InstanceData[] instanceDataArray;   // Array for the Performance (List was slower by like 30%)
        private int instanceNumber;

        // Spritesheet
        private Texture2D spriteSheet;
        private bool hasSpritesheet;
        private int spritesheetWidth;
        private int spritesheetHeight;

        // Matrix like spritebatch or userdefined
        private Matrix transformMatrix;
        private Matrix likeSpritebatchEmptyMatrix;

        // Throw error
        bool beginCalled;
        
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
                RectangleXY = RectangleSqueezer(rectangle.X, rectangle.Y);
                RectangleWH = RectangleSqueezer(rectangle.Width, rectangle.Height);
                Position = pos;
                Scale = scale;
            }

            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 1),  //4 byte TEXCOORD1
                new VertexElement(4, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 2),  //4 byte TEXCOORD1
                new VertexElement(8, VertexElementFormat.Color, VertexElementUsage.Color, 1),               //4 byte COLOR1
                new VertexElement(12, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3), //4 byte TEXCOORD2
                new VertexElement(16, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 4), //4 byte TEXCOORD3
                new VertexElement(20, VertexElementFormat.Vector2, VertexElementUsage.Position, 1),         //8 byte POSITION1
                new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 5) //8 byte TEXCOORD4
            );

            /// <summary>
            /// Render Rectangles are not bigger than 65k
            /// Bitshifts 2 values together for a smaler vertexbuffer
            /// </summary>
            /// <returns></returns>            
            public static int RectangleSqueezer(int x, int y)
            {
                // If x and y bigger than max bits on the right side = 65535
                if (x > 65535)
                {
                    x = 65535; // 0000 0000 0000 0000 1111 1111 1111 1111
                }
                if (y > 65535)
                {
                    y = 65535; // 0000 0000 0000 0000 1111 1111 1111 1111
                }

                // Shifts the bits of x to the left
                int value = x << 16; // 1111 1111 1111 1111 0000 0000 0000 0000 
                                     //                                     | x           y           value
                value |= y;          // overrides all with the second Value | 1010 0000 | 0000 1101 = 1010 1101

                return value;
            }

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
        }

        /// <summary>
        /// This class allows Instancing of Rectangles out of a Spritesheet.
        /// It draws always from back to front.
        /// Requires DX11 (DX10) and GraphicsProfile.HiDef
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
            emptyBuffers = true;            
            
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

            // Erstellt den Index-Array für zwei Dreiecke (zusammen ein Rechteck)
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
        /// This shoud be called if the resolution changes
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
        /// numberOfElements sets the array capacity (standart = 1).
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
            if (numberOfElements > 0)
            {
                // Array size increase if number of Elements are bigger
                if (instanceDataArray.Length < numberOfElements)
                {
                    Array.Resize(ref instanceDataArray, numberOfElements);
                }              
                instanceNumber = 0;
            }
            // Resize the Array (Capacity not known)
            else
            {
                Array.Resize(ref instanceDataArray, 1);
                instanceNumber = 0;
            }
        }

        /// <summary>
        /// Starts collecting the “drawcalls” in an array before sending them to the graphics card in a (Vetex)Instancing buffer.
        /// numberOfElements sets the array capacity (standart = 1).
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
            if (numberOfElements > 0)
            {
                // Array size increase if number of Elements are bigger
                if (instanceDataArray.Length < numberOfElements)
                {
                    Array.Resize(ref instanceDataArray, numberOfElements);
                }
                instanceNumber = 0;
            }
            // Resize the Array (Capacity not known)
            else
            {
                Array.Resize(ref instanceDataArray, 1);
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

        #region Draw Top Left

        /// <summary>
        /// Draws from the top left corner of the sprite(sheet)
        /// Adds the complete sprite/spritesheet in the draw array
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
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(spritesheetWidth, spritesheetHeight);
            instanceDataArray[instanceNumber].Position = new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds the complete sprite/spritesheet in the draw array
        /// </summary>        
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
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(spritesheetWidth, spritesheetHeight);
            instanceDataArray[instanceNumber].Position = position + new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds the complete sprite/spritesheet in the draw array
        /// </summary>        
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
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(spritesheetWidth, spritesheetHeight);
            instanceDataArray[instanceNumber].Position = position + new Vector2(spritesheetWidth / 2, spritesheetHeight / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        /// </summary>        
        public void DrawTopLeft(Vector2 position, Rectangle rectangle)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        /// The element can be tinted in a Color
        /// </summary>   
        /// <param name="position">The top left position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>        
        /// <param name="color">Tint color of the sprite</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
            instanceDataArray[instanceNumber].Scale = Vector2.One;
            
            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds a spritesheet element to the drawlist
        /// The element can be Scaled
        /// </summary>
        /// <param name="position">The top left position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>        
        /// <param name="scale">Scale of the sprite in x & y (-scale need to be equal, -1x,-1y)</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Vector2 scale)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the top left corner of the Sprite(sheet)
        /// Adds a spritesheet element to the drawlist
        /// The element can be tinted in a Color
        /// The element can be Scaled
        /// </summary>
        /// <param name="position">The top left position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>        
        /// <param name="scale">Scale of the sprite in x & y (-scale need to be equal, -1x,-1y)</param>
        /// <param name="color">Tint Color of the sprite</param>
        public void DrawTopLeft(Vector2 position, Rectangle rectangle, Vector2 scale, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position + new Vector2(rectangle.Width / 2 * MathF.Abs(scale.X), rectangle.Height / 2 * MathF.Abs(scale.Y));
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }
        #endregion

        #region NormalDraws
        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds the complete sprite/spritesheet in the Draw List
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
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(spritesheetWidth, spritesheetHeight);
            instanceDataArray[instanceNumber].Position = new Vector2(0);
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }


        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds the complete Sprite(sheet) in the draw array
        /// </summary>
        /// <param name="position">The middle position of the element</param> 
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
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(spritesheetWidth, spritesheetHeight);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        /// </summary>        
        /// <param name="position">The middle position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>
        public void Draw(Vector2 position, Rectangle rectangle)
        {   
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = Color.White;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        /// The element can be tinted in a Color
        /// </summary>        
        /// <param name="position">The middle position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>
        /// <param name="color">Tint Color of the sprite</param>
        public void Draw(Vector2 position, Rectangle rectangle, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = 0f;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        /// The element can be rotated
        /// The element can be tinted in a Color
        /// </summary>
        /// <param name="position">The middle position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>
        /// <param name="rotation">Rotation in radians around the sprite middlepoint</param>        
        /// <param name="color">Tint Color of the sprite</param>
        public void Draw(Vector2 position, Rectangle rectangle, float rotation, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = rotation;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = Vector2.One;

            instanceNumber++;
        }

        /// <summary>
        /// Draws from the middlepoint of the Sprite(sheet)
        /// Adds a spritesheet element(Rectangle) to the draw array
        ///  The element can be rotated
        ///  The element can be Scaled
        ///  The element can be tinted in a Color
        /// </summary>
        /// <param name="position">The middle position of the element</param>
        /// <param name="rectangle">A rectangle from the spritesheet</param>
        /// <param name="rotation">Rotation in radians around the sprite middlepoint</param>
        /// <param name="scale">Scale of the sprite in x & y (-scale need to be equal, -1x,-1y)</param>
        /// <param name="color">Tint Color of the sprite</param>
        public void Draw(Vector2 position, Rectangle rectangle, float rotation, Vector2 scale, Color color)
        {
            if (instanceNumber >= instanceDataArray.Length)
            {
                ResizeTheInstancesArray();
            }

            instanceDataArray[instanceNumber].Depth = instanceNumber;
            instanceDataArray[instanceNumber].Rotation = rotation;
            instanceDataArray[instanceNumber].Color = color;
            instanceDataArray[instanceNumber].RectangleXY = InstanceData.RectangleSqueezer(rectangle.X, rectangle.Y);
            instanceDataArray[instanceNumber].RectangleWH = InstanceData.RectangleSqueezer(rectangle.Width, rectangle.Height);
            instanceDataArray[instanceNumber].Position = position;
            instanceDataArray[instanceNumber].Scale = scale;

            instanceNumber++;
        }

        #endregion

        /// <summary>
        /// Adds the created Array from the draws methods together in a dynamic vertexbuffer(Instancingbuffer) for the single drawcall.
        /// </summary>
        public void End(GraphicsDevice graphicsDevice)
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
            if (emptyBuffers)
            {
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
            }

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
}
