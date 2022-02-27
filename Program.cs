using System.Diagnostics;
using System.Runtime.InteropServices;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Math;

public class Program : Game
{
	public static void Main(string[] args)
	{
		WindowCreateInfo windowCreateInfo = new WindowCreateInfo
		{
			WindowTitle = "Cube",
			ScreenMode = ScreenMode.Windowed,
			WindowWidth = 640,
			WindowHeight = 480,
		};

		Program p = new Program(windowCreateInfo, PresentMode.FIFORelaxed);
		p.Run();
	}

	GraphicsPipeline cubePipeline;
	RenderTarget colorTarget;
	RenderTarget depthTarget;
	Buffer vertexBuffer;
	Buffer indexBuffer;

	Stopwatch timer;

	struct Uniforms
	{
		public Matrix4x4 ViewProjection;
	}

	struct PositionColorVertex
	{
		public Vector3 Position;
		public Color Color;

		public PositionColorVertex(Vector3 position, Color color)
		{
			Position = position;
			Color = color;
		}
	}

	public Program(WindowCreateInfo windowCreateInfo, PresentMode presentMode)
		: base(windowCreateInfo, presentMode, 60, true)
	{
		ShaderModule vertShaderModule = new ShaderModule(
			GraphicsDevice,
			System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "vert.spv")
		);
		ShaderModule fragShaderModule = new ShaderModule(
			GraphicsDevice,
			System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "frag.spv")
		);

		colorTarget = RenderTarget.CreateBackedRenderTarget(
			GraphicsDevice,
			Window.Width,
			Window.Height,
			TextureFormat.R8G8B8A8,
			false
		);

		Texture depthTex = Texture.CreateTexture2D(
			GraphicsDevice,
			Window.Width,
			Window.Height,
			TextureFormat.D16,
			TextureUsageFlags.DepthStencilTarget
		);
		depthTarget = new RenderTarget(GraphicsDevice, new TextureSlice(depthTex));

		vertexBuffer = new Buffer(
			GraphicsDevice,
			BufferUsageFlags.Vertex,
			(uint) (Marshal.SizeOf<PositionColorVertex>() * 24)
		);
		indexBuffer = new Buffer(
			GraphicsDevice,
			BufferUsageFlags.Index,
			(uint) (Marshal.SizeOf<ushort>() * 36)
		);

		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		cmdbuf.SetBufferData<PositionColorVertex>(
			vertexBuffer,
			new PositionColorVertex[]
			{
				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(1f, 0f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(1f, 0f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(0f, 1f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 1f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 0f, 1f)),
				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(0f, 0f, 1f)),

				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(1f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(1f, 0.5f, 0f)),

				new PositionColorVertex(new Vector3(-1, -1, -1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(-1, -1, 1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(1, -1, 1), new Color(1f, 0f, 0.5f)),
				new PositionColorVertex(new Vector3(1, -1, -1), new Color(1f, 0f, 0.5f)),

				new PositionColorVertex(new Vector3(-1, 1, -1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(-1, 1, 1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, 1), new Color(0f, 0.5f, 0f)),
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(0f, 0.5f, 0f)),
			}
		);
		cmdbuf.SetBufferData<ushort>(
			indexBuffer,
			new ushort[]
			{
				0, 1, 2,	0, 2, 3,
				6, 5, 4,	7, 6, 4,
				8, 9, 10,	8, 10, 11,
				14, 13, 12,	15, 14, 12,
				16, 17, 18,	16, 18, 19,
				22, 21, 20,	23, 22, 20
			}
		);
		GraphicsDevice.Submit(cmdbuf);

		cubePipeline = new GraphicsPipeline(
			GraphicsDevice,
			new GraphicsPipelineCreateInfo
			{
				ViewportState = new ViewportState((int) Window.Width, (int) Window.Height),
				PrimitiveType = PrimitiveType.TriangleList,
				PipelineLayoutInfo = new GraphicsPipelineLayoutInfo(0, 0),
				RasterizerState = RasterizerState.CW_CullNone,
				MultisampleState = MultisampleState.None,
				DepthStencilState = DepthStencilState.DepthReadWrite,
				ColorBlendState = new ColorBlendState(),
				AttachmentInfo = new GraphicsPipelineAttachmentInfo
				{
					ColorAttachmentDescriptions = new ColorAttachmentDescription[]
					{
						new ColorAttachmentDescription
						{
							BlendState = ColorAttachmentBlendState.Opaque,
							Format = TextureFormat.R8G8B8A8,
							SampleCount = SampleCount.One
						}
					},
					HasDepthStencilAttachment = true,
					DepthStencilFormat = TextureFormat.D16
				},
				VertexInputState = new VertexInputState
				{
					VertexBindings = new VertexBinding[]
					{
						new VertexBinding
						{
							Binding = 0,
							InputRate = VertexInputRate.Vertex,
							Stride = (uint) Marshal.SizeOf<PositionColorVertex>()
						}
					},
					VertexAttributes = new VertexAttribute[]
					{
						new VertexAttribute
						{
							Binding = 0,
							Location = 0,
							Offset = (uint) Marshal.OffsetOf<PositionColorVertex>("Position"),
							Format = VertexElementFormat.Vector3
						},
						new VertexAttribute
						{
							Binding = 0,
							Location = 1,
							Offset = (uint) Marshal.OffsetOf<PositionColorVertex>("Color"),
							Format = VertexElementFormat.Color
						}
					}
				},
				VertexShaderState = new ShaderStageState
				{
					EntryPointName = "main",
					ShaderModule = vertShaderModule,
					UniformBufferSize = (uint) Marshal.SizeOf<Uniforms>()
				},
				FragmentShaderState = new ShaderStageState
				{
					EntryPointName = "main",
					ShaderModule = fragShaderModule,
					UniformBufferSize = 0
				}
			}
		);

		timer = Stopwatch.StartNew();
	}

	protected override void Update(System.TimeSpan dt) { }

	protected override void Draw(System.TimeSpan dt, double alpha)
	{
		Quaternion rotation = Quaternion.CreateFromYawPitchRoll(
			(float)timer.Elapsed.TotalSeconds * 2f,
			0,
			(float)timer.Elapsed.TotalSeconds * 2f
		);
		Matrix4x4 model = Matrix4x4.CreateFromQuaternion(rotation);
		Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.ToRadians(75f), (float)Window.Width / Window.Height, 0.01f, 10f);
		Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 1.5f, 4f), Vector3.Zero, Vector3.Up);
		Matrix4x4 viewProjection = model * view * proj;
		Uniforms uniforms = new Uniforms { ViewProjection = viewProjection };

		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();
		cmdbuf.BeginRenderPass(
			new DepthStencilAttachmentInfo
			{
				DepthStencilTarget = depthTarget,
				DepthStencilValue = new DepthStencilValue
				{
					Depth = 1f,
					Stencil = 0
				},
				LoadOp = LoadOp.Clear,
				StoreOp = StoreOp.DontCare
			},
			new ColorAttachmentInfo
			{
				RenderTarget = colorTarget,
				ClearColor = Color.CornflowerBlue,
				LoadOp = LoadOp.Clear,
				StoreOp = StoreOp.Store
			}
		);
		cmdbuf.BindGraphicsPipeline(cubePipeline);
		cmdbuf.BindVertexBuffers(0, new BufferBinding(vertexBuffer, 0));
		cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
		uint vertexParamOffset = cmdbuf.PushVertexShaderUniforms<Uniforms>(uniforms);
		cmdbuf.DrawIndexedPrimitives(0, 0, 12, vertexParamOffset, 0);
		cmdbuf.EndRenderPass();

		cmdbuf.QueuePresent(colorTarget.TextureSlice, Filter.Linear, Window);
		GraphicsDevice.Submit(cmdbuf);
	}

	protected override void OnDestroy() { }
}
