using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

public partial class main : Node3D
{
	[Export]private Label voxelLabel;
	[Export]private Label fpsLabel;
	[Export]private Gpu gpuInstance;
	[Export]private Button gpuButton;
	[Export]private Label gpuLabel;
	[Export]private Cpu cpuInstance;
	[Export]private Button cpuButton;
	[Export]private Label cpuLabel;
	[Export]private Button clearButton;

	[Export] private Material material;
	[Export] private FastNoiseLite fastNoise;

	[Export] private int iso_level;
	[Export] private int noise_seed = 0;
	[Export(PropertyHint.Range, "8,256,8")] private int resolution = 16;
	[Export(PropertyHint.Range, "8,256,8")] private  int size = 16;


	[Export] public Vector3 offset = Vector3.Zero;
	private float[,,] voxel;


    public override void _PhysicsProcess(double delta)
    {
		fpsLabel.Text = "FPS:" + Engine.GetFramesPerSecond().ToString();
    }
    public override void _Ready()
	{
		gpuButton.Pressed += StartGpu;
		cpuButton.Pressed += StartCpu;
		clearButton.Pressed += Clear;
		
		gpuInstance.iso_level = iso_level;
		gpuInstance.resolution = resolution;
		gpuInstance.size = size;
		gpuInstance.offset = offset;
		gpuInstance.material = material;

		gpuInstance.Set_buffers();

		cpuInstance.iso_level = iso_level;
		cpuInstance.resolution = resolution;
		cpuInstance.size = size;
		cpuInstance.offset = offset;
		cpuInstance.material = material;


		gpuButton.Disabled = true;
		cpuButton.Disabled = true;
		clearButton.Disabled = true;
		Task tsk = new(()=>{Set_Voxel();});
		tsk.Start();
	}
    private void Set_Voxel()
    {
		float index = 0;
		int range = resolution/2;
		voxel = new float[resolution+1,resolution+1,resolution+1];
		
		for(int y = -range; y <= range ; y++)
		{
			for(int z = -range; z <= range ; z++)
			{
				for(int x = -range; x <= range ; x++)
				{
					Vector3 local_point = new Vector3(x, y, z) * size / resolution;
					Vector3 global_point = local_point + offset;

					float densety = fastNoise.GetNoise3Dv(global_point);
					
					if(x == -range || y == -range || z == -range || x == range || y == range || z == range)
					{
						voxel[x+range,y+range,z+range] = -1;
					}
					else
					{
						voxel[x+range,y+range,z+range] = densety;
					}
				}
				CallDeferred("voxelPercenteg",index);
			}
			
			index++;
		}
		CallDeferred("voxelDone");
    }

	private void voxelPercenteg(int index)
	{
		voxelLabel.Text = "voxel:"+Mathf.Round(index/resolution*100).ToString()+"%";
	}
	private void voxelDone()
	{
		gpuInstance.voxel = voxel;
		cpuInstance.voxel = voxel;

		gpuButton.Disabled = false;
		cpuButton.Disabled = false;
		clearButton.Disabled = false;
	}

    private void StartGpu()
    {
        gpuButton.Disabled = true;
		cpuButton.Disabled = true;
		clearButton.Disabled = true;
		ulong start = Time.GetTicksMsec();
		
		gpuInstance.Create_chunk();

		ulong end = Time.GetTicksMsec();
		gpuLabel.Text = (end - start).ToString() + "ms";
		clearButton.Disabled = false;
    }


    private void StartCpu()
    {
        gpuButton.Disabled = true;
		cpuButton.Disabled = true;
		clearButton.Disabled = true;
		ulong start = Time.GetTicksMsec();

		cpuInstance.Create_chunk();

		ulong end = Time.GetTicksMsec();
		cpuLabel.Text = (end - start).ToString() + "ms";
		
		clearButton.Disabled = false;
    }

	private ulong GetTime()
	{
		return Time.GetTicksMsec();
	}
	private void ChangeText(string text)
	{
		gpuLabel.Text = text;

	}


    public void Clear()
	{
        gpuButton.Disabled = true;
		cpuButton.Disabled = true;
		clearButton.Disabled = true;

		cpuInstance.mesh.ClearSurfaces();
		gpuInstance.mesh.ClearSurfaces();

        gpuButton.Disabled = false;
		cpuButton.Disabled = false;
		clearButton.Disabled = false;
	}



}
