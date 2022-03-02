using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
	GraphicsPipeline skyboxPipeline;
	Texture depthTexture;
	Buffer cubeVertexBuffer;
	Buffer skyboxVertexBuffer;
	Buffer indexBuffer;
	Texture skyboxTexture;
	Sampler skyboxSampler;
	bool finishedLoading;

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

	struct PositionVertex
	{
		public Vector3 Position;

		public PositionVertex(Vector3 position)
		{
			Position = position;
		}
	}

	void LoadCubemap(CommandBuffer cmdbuf, string[] imagePaths)
	{
		System.IntPtr textureData;
		int w, h, numChannels;

		for (uint i = 0; i < imagePaths.Length; i++)
		{
			textureData = RefreshCS.Refresh.Refresh_Image_Load(
				imagePaths[i],
				out w,
				out h,
				out numChannels
			);
			cmdbuf.SetTextureData(
				new TextureSlice(
					skyboxTexture,
					new Rect(0, 0, w, h),
					0,
					i
				),
				textureData,
				(uint) (w * h * 4) // w * h * numChannels does not work
			);
			RefreshCS.Refresh.Refresh_Image_Free(textureData);
		}
	}

	public Program(WindowCreateInfo windowCreateInfo, PresentMode presentMode)
		: base(windowCreateInfo, presentMode, 60, true)
	{
		string baseContentPath = Path.Combine(
			System.AppDomain.CurrentDomain.BaseDirectory,
			"Content"
		);

		ShaderModule cubeVertShaderModule = new ShaderModule(
			GraphicsDevice,
			Path.Combine(baseContentPath, "cube_vert.spv")
		);
		ShaderModule cubeFragShaderModule = new ShaderModule(
			GraphicsDevice,
			Path.Combine(baseContentPath, "cube_frag.spv")
		);

		ShaderModule skyboxVertShaderModule = new ShaderModule(
			GraphicsDevice,
			Path.Combine(baseContentPath, "skybox_vert.spv")
		);
		ShaderModule skyboxFragShaderModule = new ShaderModule(
			GraphicsDevice,
			Path.Combine(baseContentPath, "skybox_frag.spv")
		);

		depthTexture = Texture.CreateTexture2D(
			GraphicsDevice,
			Window.Width,
			Window.Height,
			TextureFormat.D16,
			TextureUsageFlags.DepthStencilTarget
		);

		skyboxTexture = Texture.CreateTextureCube(
			GraphicsDevice,
			2048,
			TextureFormat.R8G8B8A8,
			TextureUsageFlags.Sampler
		);
		skyboxSampler = new Sampler(GraphicsDevice, new SamplerCreateInfo());

		cubeVertexBuffer = new Buffer(
			GraphicsDevice,
			BufferUsageFlags.Vertex,
			(uint) (Marshal.SizeOf<PositionColorVertex>() * 24)
		);
		skyboxVertexBuffer = new Buffer(
			GraphicsDevice,
			BufferUsageFlags.Vertex,
			(uint) (Marshal.SizeOf<PositionVertex>() * 24)
		);
		indexBuffer = new Buffer(
			GraphicsDevice,
			BufferUsageFlags.Index,
			(uint) (Marshal.SizeOf<ushort>() * 36)
		);

		Task loadingTask = Task.Run(() => UploadGPUAssets(baseContentPath));

		cubePipeline = new GraphicsPipeline(
			GraphicsDevice,
			new GraphicsPipelineCreateInfo
			{
				ViewportState = new ViewportState((int) Window.Width, (int) Window.Height),
				PrimitiveType = PrimitiveType.TriangleList,
				RasterizerState = RasterizerState.CW_CullBack,
				MultisampleState = MultisampleState.None,
				DepthStencilState = DepthStencilState.DepthReadWrite,
				AttachmentInfo = new GraphicsPipelineAttachmentInfo
				{
					ColorAttachmentDescriptions = new ColorAttachmentDescription[]
					{
						new ColorAttachmentDescription
						{
							BlendState = ColorAttachmentBlendState.Opaque,
							Format = GraphicsDevice.GetSwapchainFormat(Window),
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
				VertexShaderInfo = new GraphicsShaderInfo
				{
					EntryPointName = "main",
					ShaderModule = cubeVertShaderModule,
					UniformBufferSize = (uint) Marshal.SizeOf<Uniforms>(),
					SamplerBindingCount = 0
				},
				FragmentShaderInfo = new GraphicsShaderInfo
				{
					EntryPointName = "main",
					ShaderModule = cubeFragShaderModule,
					UniformBufferSize = 0,
					SamplerBindingCount = 0
				}
			}
		);

		skyboxPipeline = new GraphicsPipeline(
			GraphicsDevice,
			new GraphicsPipelineCreateInfo
			{
				ViewportState = new ViewportState((int) Window.Width, (int) Window.Height),
				PrimitiveType = PrimitiveType.TriangleList,
				RasterizerState = RasterizerState.CW_CullNone,
				MultisampleState = MultisampleState.None,
				DepthStencilState = DepthStencilState.DepthReadWrite,
				AttachmentInfo = new GraphicsPipelineAttachmentInfo
				{
					ColorAttachmentDescriptions = new ColorAttachmentDescription[]
					{
						new ColorAttachmentDescription
						{
							BlendState = ColorAttachmentBlendState.Opaque,
							Format = GraphicsDevice.GetSwapchainFormat(Window),
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
							Stride = (uint) Marshal.SizeOf<PositionVertex>()
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
						}
					}
				},
				VertexShaderInfo = new GraphicsShaderInfo
				{
					EntryPointName = "main",
					ShaderModule = skyboxVertShaderModule,
					UniformBufferSize = (uint) Marshal.SizeOf<Uniforms>(),
					SamplerBindingCount = 0
				},
				FragmentShaderInfo = new GraphicsShaderInfo
				{
					EntryPointName = "main",
					ShaderModule = skyboxFragShaderModule,
					UniformBufferSize = 0,
					SamplerBindingCount = 1
				}
			}
		);

		timer = Stopwatch.StartNew();
	}

	private void UploadGPUAssets(string baseContentPath)
	{
		System.Console.WriteLine("Loading...");

		// Begin submitting resource data to the GPU.
		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();

		cmdbuf.SetBufferData<PositionColorVertex>(
			cubeVertexBuffer,
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
				new PositionColorVertex(new Vector3(1, 1, -1), new Color(0f, 0.5f, 0f))
			}
		);

		cmdbuf.SetBufferData<PositionVertex>(
			skyboxVertexBuffer,
			new PositionVertex[]
			{
				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(10, -10, -10)),
				new PositionVertex(new Vector3(10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, -10)),

				new PositionVertex(new Vector3(-10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(-10, 10, 10)),

				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(-10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, 10)),
				new PositionVertex(new Vector3(-10, -10, 10)),

				new PositionVertex(new Vector3(10, -10, -10)),
				new PositionVertex(new Vector3(10, 10, -10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),

				new PositionVertex(new Vector3(-10, -10, -10)),
				new PositionVertex(new Vector3(-10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, 10)),
				new PositionVertex(new Vector3(10, -10, -10)),

				new PositionVertex(new Vector3(-10, 10, -10)),
				new PositionVertex(new Vector3(-10, 10, 10)),
				new PositionVertex(new Vector3(10, 10, 10)),
				new PositionVertex(new Vector3(10, 10, -10))
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

		LoadCubemap(cmdbuf, new string[]
		{
			Path.Combine(baseContentPath, "right.png"),
			Path.Combine(baseContentPath, "left.png"),
			Path.Combine(baseContentPath, "top.png"),
			Path.Combine(baseContentPath, "bottom.png"),
			Path.Combine(baseContentPath, "front.png"),
			Path.Combine(baseContentPath, "back.png"),
		});

		GraphicsDevice.Submit(cmdbuf);

		finishedLoading = true;
		System.Console.WriteLine("Finished loading!");
	}

	protected override void Update(System.TimeSpan dt) { }

	protected override void Draw(System.TimeSpan dt, double alpha)
	{
		Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.ToRadians(75f), (float)Window.Width / Window.Height, 0.01f, 100f);
		Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 1.5f, 4f), Vector3.Zero, Vector3.Up);
		Uniforms skyboxUniforms = new Uniforms { ViewProjection = view * proj };

		Quaternion rotation = Quaternion.CreateFromYawPitchRoll(
			(float)timer.Elapsed.TotalSeconds * 2f,
			0,
			(float)timer.Elapsed.TotalSeconds * 2f
		);
		Matrix4x4 model = Matrix4x4.CreateFromQuaternion(rotation);
		Matrix4x4 cubeModelViewProjection = model * view * proj;
		Uniforms cubeUniforms = new Uniforms { ViewProjection = cubeModelViewProjection };

		CommandBuffer cmdbuf = GraphicsDevice.AcquireCommandBuffer();

		Texture? swapchainTexture = cmdbuf.AcquireSwapchainTexture(Window);

		if (swapchainTexture != null)
		{
			if (!finishedLoading)
			{
				float sine = (float) System.Math.Abs(System.Math.Sin(timer.Elapsed.TotalSeconds));
				Color clearColor = new Color(sine, sine, sine);

				// Just show a clear screen.
				cmdbuf.BeginRenderPass(
					new DepthStencilAttachmentInfo(depthTexture, new DepthStencilValue(1f, 0)),
					new ColorAttachmentInfo(swapchainTexture, clearColor)
				);
				cmdbuf.EndRenderPass();
			}
			else
			{
				cmdbuf.BeginRenderPass(
					new DepthStencilAttachmentInfo(depthTexture, new DepthStencilValue(1f, 0)),
					new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
				);

				// Draw cube
				cmdbuf.BindGraphicsPipeline(cubePipeline);
				cmdbuf.BindVertexBuffers(0, new BufferBinding(cubeVertexBuffer, 0));
				cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
				uint vertexParamOffset = cmdbuf.PushVertexShaderUniforms<Uniforms>(cubeUniforms);
				cmdbuf.DrawIndexedPrimitives(0, 0, 12, vertexParamOffset, 0);

				// Draw skybox
				cmdbuf.BindGraphicsPipeline(skyboxPipeline);
				cmdbuf.BindVertexBuffers(0, new BufferBinding(skyboxVertexBuffer, 0));
				cmdbuf.BindIndexBuffer(indexBuffer, IndexElementSize.Sixteen);
				cmdbuf.BindFragmentSamplers(new TextureSamplerBinding(skyboxTexture, skyboxSampler));
				vertexParamOffset = cmdbuf.PushVertexShaderUniforms<Uniforms>(skyboxUniforms);
				cmdbuf.DrawIndexedPrimitives(0, 0, 12, vertexParamOffset, 0);

				cmdbuf.EndRenderPass();
			}
		}

		GraphicsDevice.Submit(cmdbuf);
	}

	protected override void OnDestroy() { }
}
