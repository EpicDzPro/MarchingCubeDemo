using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public partial class Gpu : MeshInstance3D
{
	private RenderingDevice rendering_device;
	private RDShaderFile shader_file = GD.Load<RDShaderFile>("res://gpu/glsl/compute.glsl");
	private Rid shader_RID;
	private Rid pipline_RID;
	private Rid uniform_RID;
	//buffers
	private Rid traingle_buffer_RID;
	private Rid parameters_buffer_RID;
	private Rid voxel_buffer_RID;
	//parameters
	public int iso_level;
	public int resolution;
	public int size;
	public Vector3 offset = Vector3.Zero;
	public float[,,] voxel;

	public Material material;
	public ArrayMesh mesh = new ArrayMesh();
	public Godot.Collections.Array mesh_data = new Godot.Collections.Array();
	private Vector3[] vertices;
	private Vector3[] normals;
	private System.Threading.Mutex mute = new System.Threading.Mutex();
	


    public override void _Ready()
    {
        Mesh = mesh;
		mesh_data.Resize((int)Mesh.ArrayType.Max);
    }

    public void Set_buffers()
    {
		
		rendering_device = RenderingServer.CreateLocalRenderingDevice();
		RDShaderSpirV shader_spirv = shader_file.GetSpirV();
		shader_RID = rendering_device.ShaderCreateFromSpirV(shader_spirv);

		//////////////////////////////TRIANGLE_BUFFER///////////////////////////////////////////////
		uint traingle_bytes = (uint)Mathf.Pow(resolution,3) * 5 * 4 * 4 * 3;
		//size^3 * max triangles per voxel * bytes per float * vec3 per traingle * floats per vec3
		traingle_buffer_RID = rendering_device.StorageBufferCreate(traingle_bytes);
		RDUniform traingle_uniform = new RDUniform();
		traingle_uniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
		traingle_uniform.Binding = 0 ;
		traingle_uniform.AddId(traingle_buffer_RID);

		//////////////////////////////VOXEL_BUFFER//////////////////////////////////////////////////
		byte[] voxel_bytes = new byte[sizeof(float)*(int)Mathf.Pow(resolution+1,3)];
		//size^3 * max triangles per voxel * bytes per float * vec3 per traingle * floats per vec3
		voxel_buffer_RID = rendering_device.StorageBufferCreate((uint)voxel_bytes.Length,voxel_bytes);
		RDUniform voxel_uniform = new RDUniform();
		voxel_uniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
		voxel_uniform.Binding = 1 ;
		voxel_uniform.AddId(voxel_buffer_RID);

		//////////////////////////////PARAMETERS_BUFFER/////////////////////////////////////////////
		byte[] parameters_bytes = Set_parameters();
		//size^3 * max triangles per voxel * bytes per float * vec3 per traingle * floats per vec3
		parameters_buffer_RID = rendering_device.StorageBufferCreate((uint)parameters_bytes.Length,parameters_bytes);
		RDUniform parameters_uniform = new RDUniform();
		parameters_uniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
		parameters_uniform.Binding = 2 ;
		parameters_uniform.AddId(parameters_buffer_RID);

		Godot.Collections.Array<RDUniform> uniforms = new() { traingle_uniform,voxel_uniform,parameters_uniform};
		uniform_RID = rendering_device.UniformSetCreate(uniforms,shader_RID,0);
		pipline_RID = rendering_device.ComputePipelineCreate(shader_RID);
    }


    public void Create_chunk()
    {

		
		int v = resolution;
		float[,,] vox = new float[v+1,v+1,v+1];
		offset *= v;

		for(int y = 0; y <= v ; y++)
		{
			for(int z = 0; z <= v ; z++)
			{
				for(int x = 0; x <= v ; x++)
				{
					vox[x,y,z] = voxel[(int)offset.X+x,(int)offset.Y+y,(int)offset.Z+z];
				}
			}
		}

		//mesh.ClearSurfaces();

		byte[] parameters_bytes = Set_parameters();
		byte[] voxel_bytes = new byte[sizeof(float)*(int)Mathf.Pow(resolution+1,3)];
		Buffer.BlockCopy(vox,0,voxel_bytes,0,voxel_bytes.Length);

		mute.WaitOne();
		///////MainThread//////////
		rendering_device.BufferUpdate(parameters_buffer_RID,0,(uint)parameters_bytes.Length,parameters_bytes);
		rendering_device.BufferUpdate(voxel_buffer_RID,0,(uint)voxel_bytes.Length,voxel_bytes);

		long compute_list = rendering_device.ComputeListBegin();
		rendering_device.ComputeListBindComputePipeline(compute_list,pipline_RID);
		rendering_device.ComputeListBindUniformSet(compute_list,uniform_RID,0);
		rendering_device.ComputeListDispatch(compute_list,(uint)resolution/8,(uint)resolution/8,(uint)resolution/8);
		rendering_device.ComputeListEnd();

		rendering_device.Submit();
		rendering_device.Sync();

		byte[] parameters_output = rendering_device.BufferGetData(parameters_buffer_RID);
		byte[] traingle_output = rendering_device.BufferGetData(traingle_buffer_RID);
		///////MainThread//////////
		mute.ReleaseMutex();


		int[] parameters = new int[10];
		Buffer.BlockCopy(parameters_output,0,parameters,0,parameters_output.Length);
		float[] traingles = new float[parameters[0]*4*3];
		Buffer.BlockCopy(traingle_output,0,traingles,0,parameters[0]*4*3*sizeof(float));

		
		Vector3[] vertices = new Vector3[parameters[0]*3];
		Vector3[] normals = new Vector3[parameters[0]*3];
		
		//GD.Print(parameters[0]);
		for(int t = 0 ; t < parameters[0] ; t++)
		{
			int i = t * 12;
			Vector3 normal =      new Vector3(traingles[i + 9],traingles[i + 10],traingles[i + 11]);
			
			vertices[t * 3 + 0] = new Vector3(traingles[i + 0],traingles[i + 1],traingles[i + 2]);
			vertices[t * 3 + 1] = new Vector3(traingles[i + 3],traingles[i + 4],traingles[i + 5]);
			vertices[t * 3 + 2] = new Vector3(traingles[i + 6],traingles[i + 7],traingles[i + 8]);
			
			normals[t * 3 + 0] = normal;
			normals[t * 3 + 1] = normal;
			normals[t * 3 + 2] = normal;

		}
		
		if(parameters[0] != 0)
		{
			mesh_data[(int)Mesh.ArrayType.Vertex] = vertices;
			mesh_data[(int)Mesh.ArrayType.Normal] = normals;
			//CallDeferred("SetMesh");
			CallDeferredThreadGroup("SetMesh");
		}
    }

	private void SetMesh()
	{
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,mesh_data);
		mesh.SurfaceSetMaterial(0,material);
	}


    private byte[] Set_parameters()
    {
		int[] parameters = new int[7];
		parameters[0] = 0;
		parameters[1] = iso_level;
		parameters[2] = resolution;
		parameters[3] = size;
		parameters[4] = (int)offset.X;
		parameters[5] = (int)offset.Y;
		parameters[6] = (int)offset.Z;
		byte[] parameters_bytes = new byte[parameters.Length * sizeof(int)];
		Buffer.BlockCopy(parameters,0,parameters_bytes,0,parameters_bytes.Length);
		
        return parameters_bytes;
    }


    public override void _ExitTree()
	{
		rendering_device.FreeRid(traingle_buffer_RID);
		rendering_device.FreeRid(voxel_buffer_RID);
		rendering_device.FreeRid(parameters_buffer_RID);
		rendering_device.FreeRid(uniform_RID);
		rendering_device.FreeRid(pipline_RID);
		rendering_device.FreeRid(shader_RID);
		rendering_device.Free();

	}
}
