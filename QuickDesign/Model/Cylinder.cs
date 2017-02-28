using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NXOpen;
using NXOpen.UF;
using NXOpen.Features;
using NXOpenUI;
using System.Text.RegularExpressions;
using System.IO;
using NXOpen.Gateway;
using System.Data;

class NozzleCylinderBuilder:AbstractBuilder
{
    class Runner
    {
        public Vector3d direction;
        public Point3d start_point;
        public Point3d end_point;
        public int runner_index;
    }

    DataControl sql = new DataControl();
    ManifoldInfo manifold;
    bool is_rebulid;

    private List<Runner> runners;
    private int g_type; //1——气缸 2——油缸

    string g_install_type;//气缸的安装方式    
    double g_thermal_expansion_coefficient = 0.0024;

    public NozzleCylinderBuilder(ManifoldInfo manifold, bool is_rebulid)
    {
        this.manifold = manifold;
        this.is_rebulid = is_rebulid;
        runners = new List<Runner>();
        g_type = 1;
    }

    public override void Commit()
    {
        //删除所有气缸
        NXFunction.DeleteBodies("CYLINDER");
        NXFunction.DeleteBodies("SUB_CYLINDER");
        NXFunction.DeleteBodies("FIXED_BLOCK");
        NXFunction.DeleteBodies("SUB_FIXED_BLOCK");
        NXFunction.DeleteBodies("VALVE2");
        NXFunction.DeleteBodies("GASLINE");

        //初始化
        string file_name = NXFunction.GetFileName();
        string host_name = sql.hostName;

        g_type = 1;//气缸
        g_install_type = "分体式";
        g_thermal_expansion_coefficient = 0.0024;

        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        //0——得到所有流道
        GetRunners();

        //1——热嘴图块
        List<Block> nozzle_blocks = sql.NozzleBlock();
        if (nozzle_blocks.Count == 0)
        {
            List<Block> nozzle_cylinders = sql.CylinderBlock();
            //2A——插入气缸 油缸
            for (int i = 0; i < nozzle_cylinders.Count; i++)
            {
                try
                {
                    ImportCylinder(nozzle_cylinders[i]);
                }
                catch (Exception ex)
                {
                    UI.GetUI().NXMessageBox.Show("", NXMessageBox.DialogType.Error, ex.ToString());
                }
            }
        }
        else
        {
            //2B——插入热嘴和气缸 油缸
            for (int i = 0; i < nozzle_blocks.Count; i++)
            {
                try
                {
                    if (!is_rebulid) ImportNozzle(nozzle_blocks[i]);
                    Block cylinder_block = sql.GetCylinderBlock(nozzle_blocks[i]);

                    ImportCylinderWithoutNozzle(cylinder_block);
                }
                catch (Exception ex)
                {
                    UI.GetUI().NXMessageBox.Show("", NXMessageBox.DialogType.Error, ex.ToString());
                }
            }
        }

        //3——气路油路
        GasLine();

        //4——隐藏假体
        List<Body> sub_bodies = NXFunction.GetBodiesByName("SUB_NOZZLE2-");
        NXFunction.HideBodies(sub_bodies, 1);

        //工艺参数
        Body body = NXFunction.GetBodyByName("MANIFOLD");
        body.SetAttribute("工艺参数", "点数", nozzle_blocks.Count.ToString());
    }

    private void GetRunners()
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        runners.Clear();

        //0——流道图层编号
        int runner_layer = NXFunction.GetLayerNum("2runner");
        if (runner_layer == -1) return;

        NXOpen.Layer.LayerManager layer_manager = workPart.Layers;
        NXObject[] objects1 = layer_manager.GetAllObjectsOnLayer(runner_layer);
        int runner_count = objects1.Length;
        if (runner_count == 0) return;

        //1——得到vector_runners 和vector_runner_points
        for (int i = 0; i < runner_count; i++)
        {
            Curve spline1 = (Curve)objects1[i];
            if (spline1 == null)
                continue;
            if (!NXFunction.IsCurve(spline1, 3)) continue;

            Line line1 = (Line)objects1[i];
            Direction direction1;
            direction1 = workPart.Directions.CreateDirection(line1,Sense.Forward, SmartObject.UpdateOption.WithinModeling);
            Vector3d vector3d1 = direction1.Vector;
            Point3d start_point = line1.StartPoint;
            Point3d end_point = line1.EndPoint;

            Runner runner = new Runner();
            runner.direction = vector3d1;
            runner.start_point = start_point;
            runner.end_point = end_point;
            runner.runner_index = i;

            runners.Add(runner);
        }
    }

    private void ImportNozzle(Block block)
    {
        try
        {
            string path_nozzle = block.file_path;
            Point3d point_nozzle = new Point3d(block.X, block.Y, -manifold.ManifoldH); 
            //插入
            NXFunction.ImportAndRoation(path_nozzle, point_nozzle, block.Angle, 1);
            //编号
            NXFunction.NumberingPart(block.index, "NOZZLE", point_nozzle, block.Angle, -1);
            NXFunction.NumberingPart(block.index, "SUB_NOZZLE1", point_nozzle, 0, 0);
            NXFunction.NumberingPart(block.index, "SUB_NOZZLE2", point_nozzle, 0, 0);
        }
        catch (Exception ex)
	    {
            string title = block.index.ToString() + "个气缸出错:";
            UI.GetUI().NXMessageBox.Show(title, NXMessageBox.DialogType.Error, ex.ToString());
        }
    }

    private void ImportCylinder(Block nozzle_block)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        Block cylinder_block = sql.GetCylinderBlock(nozzle_block);
        if (cylinder_block.assembly_id == "") return;

        if (cylinder_block.assembly_name == "分体式气缸" || cylinder_block.assembly_name == "整体式气缸")
            g_type = 1;
        else//油缸
            g_type = 2;

        if (cylinder_block.assembly_name == "分体式气缸" || cylinder_block.assembly_name == "分体式油缸")
        {
            g_install_type = "分体式";
            ImportSplitCylinder(cylinder_block);
        }
        else
        {
            g_install_type = "整体式";
            ImportManifoldCylinder(cylinder_block);
        }

    }

    private void ImportCylinderWithoutNozzle(Block cylinder_block)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        if (cylinder_block.assembly_id == "") return;

            if (cylinder_block.assembly_name == "分体式气缸" || cylinder_block.assembly_name == "整体式气缸")
                g_type = 1;
            else//油缸
                g_type = 2;

            if (cylinder_block.assembly_name == "分体式气缸" || cylinder_block.assembly_name == "分体式油缸")
            {
                g_install_type = "分体式";
                ImportSplitCylinder(cylinder_block);
            }
            else
            {
                g_install_type = "整体式";
                ImportManifoldCylinder(cylinder_block);
            }
        }

    //分体式气缸/油缸
    private void ImportSplitCylinder(Block block)
    {
        string path_cylinder = block.file_path;
        Point3d point_cylinder = new Point3d(block.X, block.Y, manifold.CylinderH + manifold.TopH);

        //插入
        NXFunction.ImportAndRoation(path_cylinder, point_cylinder, 0, 1);
        Body cylinder = NXFunction.GetBodyByName("CYLINDER");
        Body subCylinder = NXFunction.GetBodyByName("SUB_CYLINDER");
        Body fixedBlock = NXFunction.GetBodyByName("FIXED_BLOCK");
        Body subFixedBlock = NXFunction.GetBodyByName("SUB_FIXED_BLOCK");
        
        //旋转角度
        NXFunction.RotatingPart(block.Angle, point_cylinder, cylinder);
        NXFunction.RotatingPart(block.Angle, point_cylinder, subCylinder);

        Vector3d dir_default = new Vector3d(0, 1, 0);
        Vector3d dir_target = GetRunnerDir(block);
        Point3d p = new Point3d(block.X, block.Y, 0);
        NXFunction.RotatingPart(dir_default, dir_target, p, fixedBlock);
        NXFunction.RotatingPart(dir_default, dir_target, p, subFixedBlock);

        //高度移动量 和 热膨胀移动
        
        if (!cylinder.HasUserAttribute("H", NXObject.AttributeType.Any, -1))
            cylinder.SetUserAttribute("H", -1, "80", Update.Option.Later);
        string strH = cylinder.GetStringUserAttribute("H", -1);
        double cylinder_Height = double.Parse(strH);//气缸原来的高度	

        double X = g_thermal_expansion_coefficient * (-block.X);
        double Y = g_thermal_expansion_coefficient * (-block.Y);
        double Z = cylinder_Height + 15 - manifold.CylinderH - manifold.TopH;//15:3D PRT中默认隔热介子高度

        fixedBlock.MoveBodyByIncrement(X, Y, Z);
        subFixedBlock.MoveBodyByIncrement(X, Y, Z);

        //属性 assemblyID在BOM.DLL中将转换为一系列的物料编码，将被删除
        cylinder.SetAttribute("基本参数", "assembly_id", block.assembly_id);
        cylinder.SetAttribute("基本参数", "安装方式", "分体式");
        cylinder.SetAttribute("基本参数", "组件类型", block.assembly_name);

        //编号
        cylinder.NumberingPart(block.index,point_cylinder, block.Angle, -1);
        subCylinder.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        fixedBlock.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        subFixedBlock.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        NXFunction.GetBodyByName("VALVE2").NumberingPart(block.index, point_cylinder, block.Angle, -1);
    }

    //气缸所在流道的方向
    private Vector3d GetRunnerDir(Block block)
    {
        Vector3d dir = new Vector3d(1, 0, 0);
        Point3d p=new Point3d(block.X, block.Y, 0);
        for (int i = 0; i < runners.Count; i++)
        {
            if (NXFunction.IsP1inP2P3_Plus(p, runners[i].start_point, runners[i].end_point))
            {
                dir.X = runners[i].direction.X;
                dir.Y = runners[i].direction.Y;
                break;
            }
        }
        return dir;
    }

    private void GasLine()
    {
        if (g_install_type == "整体式") return;

        int int_layer = NXFunction.GetLayerNum("7gasline");
        if (int_layer == -1) return;
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        List<Body> bodies = new List<Body>();
        List<DisplayableObject> dispalys = new List<DisplayableObject>();

        double gasline_H1, gasline_H2;
        gasline_H1 = manifold.CylinderH + manifold.TopH - 10 - 15;
        gasline_H2 = manifold.CylinderH + manifold.TopH - 35 - 15;

        workPart.Layers.SetState(42,NXOpen.Layer.State.WorkLayer);
        NXOpen.Layer.LayerManager layer_manager = workPart.Layers;
        NXObject[] objects1 = layer_manager.GetAllObjectsOnLayer(int_layer);
        int gasline_count = objects1.Length;

        Feature nullFeatures_Feature = null;
        try
        {
            for (int i = 0; i < gasline_count; i++)
            {
                Curve curve = (Curve)objects1[i];
                if (curve == null) continue;
                if (NXFunction.IsCurve(curve, 3) == false) continue;

                CylinderBuilder cylinderBuilder1 = workPart.Features.CreateCylinderBuilder(nullFeatures_Feature);
                //方向和位置
                Line line1 = (Line)objects1[i];
                Direction direction1;
                direction1 = workPart.Directions.CreateDirection(line1, Sense.Forward, SmartObject.UpdateOption.WithinModeling);
                Vector3d vector3d1 = direction1.Vector;
                Point3d startPoint = line1.StartPoint;
                Point3d endPoint = line1.EndPoint;

                startPoint.Z = startPoint.Z + gasline_H1;
                endPoint.Z = endPoint.Z + gasline_H1;

                cylinderBuilder1.Direction = vector3d1;
                cylinderBuilder1.Origin = startPoint;
                //参数设置
                double cylinder_height = Math.Sqrt(Math.Pow(startPoint.X - endPoint.X, 2) + 
                    Math.Pow(startPoint.Y - endPoint.Y, 2) + 
                    Math.Pow(startPoint.Z - endPoint.Z, 2));
                cylinderBuilder1.Diameter.RightHandSide = "8.0";
                cylinderBuilder1.Height.RightHandSide = cylinder_height.ToString();
                NXObject gas_ob = cylinderBuilder1.Commit();
                cylinderBuilder1.Destroy();
                //颜色
                string str1 = gas_ob.JournalIdentifier;
                Body body1 = (Body)(workPart.Bodies.FindObject(str1));
                //命名
                body1.SetName("GASLINE");
                bodies.Add(body1);
                dispalys.Add(body1);
            }
        }
        catch (Exception ex)
        {
            string title = "气路出错:";
            UI.GetUI().NXMessageBox.Show(title, NXMessageBox.DialogType.Error, ex.ToString());
        }

        NXFunction.SetColor(dispalys.ToArray(), 130);
        NXFunction.CopyBodyByIncrement(bodies, 0, 0, gasline_H2 - gasline_H1);
   }

    //整体式气缸油缸
    private void ImportManifoldCylinder(Block block)
    {
        string path_cylinder = block.file_path;
        Point3d point_cylinder = new Point3d(block.X, block.Y, 0);

        //插入
        NXFunction.ImportAndRoation(path_cylinder, point_cylinder, block.Angle, 1);

        //热膨胀
        double X = g_thermal_expansion_coefficient * (-block.X);
        double Y = g_thermal_expansion_coefficient * (-block.Y);
        Body cylinder = NXFunction.GetBodyByName("CYLINDER");
        Body bottom = NXFunction.GetBodyByName("SUB_CYLINDER_BOTTOM");
        Body top = NXFunction.GetBodyByName("SUB_CYLINDER_TOP");
        Body subCylinder = NXFunction.GetBodyByName("SUB_CYLINDER");
        cylinder.MoveBodyByIncrement(X, Y, 0);
        bottom.MoveBodyByIncrement(X, Y, 0);

        //属性 assemblyID在BOM.DLL中将转换为一系列的物料编码，将被删除

        cylinder.SetAttribute("基本参数", "assembly_id", block.assembly_id);
        cylinder.SetAttribute("基本参数", "安装方式", "整体式");
        cylinder.SetAttribute("基本参数", "组件类型", block.assembly_name);

        //编号
        cylinder.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        top.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        bottom.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        subCylinder.NumberingPart(block.index, point_cylinder, block.Angle, -1);
        NXFunction.GetBodyByName("VALVE2").NumberingPart(block.index, point_cylinder, block.Angle, -1);
    }

}
