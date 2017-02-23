using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NXOpen;
using NXOpen.UF;
using NXOpen.Features;
using NXOpen.Tooling;

class HeaterBuilder
{
    ManifoldInfo manifold;
    DataControl sql;
    int layerNum;

    List<HeaterSegment> segments = new List<HeaterSegment>();//所有加热线

    public HeaterBuilder(ManifoldInfo manifold)
    {
        this.manifold = manifold;

        sql = new DataControl();
        layerNum = NXFunction.GetLayerNum("3heater");
    }

    public void Commit()
    {
        if (layerNum == -1) return;

        NXFunction.DeleteBodies("TUBE");

        double totolLen = GetSegments();//得到所有加热线段和总长度

        if (totolLen == 0) return;
        double Z = -3.7;

        //1——遍历线段，获得一条条的线
        List<HeaterLine> heaterLines = new List<HeaterLine>();
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].added == true || segments[i].type != 1) continue;
            //获得中心线
            segments[i].added = true;
            HeaterLine heater_line = GetHeaterLine(segments[i].curve);
            heaterLines.Add(heater_line);
        }
        
        //1.5 发热管排序
        heaterLines.OrderBy(i => i.orderPoint.X).ThenBy(i => i.orderPoint.Y);

        //2——生成
        for (int i = 0; i < heaterLines.Count; i++)
        {
            if (heaterLines[i].curves.Count < 1) continue;
            //创建文字
            CreateText(i + 1, heaterLines[i]);
            //生成上下两层的线槽		
            Body swept = CreateSwept(i + 1, heaterLines[i]);
            swept.MirrorBody(-manifold.ManifoldH / 2);
            //生成上层的管道，并插入属性
            Body tube = CreateTube(i + 1, heaterLines[i], 6.5, 0, Z);
            //生成上层陶瓷接线盒，并和管道合并
            CreateBox(tube,i + 1, heaterLines[i].dir, heaterLines[i], Z);
            //复制生成下层加热线
            tube.CopyBodyByIncrement(0,0,-manifold.ManifoldH - 2 * Z);            
        }

        //3——后续
        Body target = NXFunction.GetBodyByName("MANIFOLD");
        List<Body> tools = NXFunction.GetBodiesByName("SWEPT-");
        target.SubPart(tools);

        target.RemoveParameters();
        //刻字
        Crave();

        //物料编码
        target.SetAttribute("工艺参数","发热管总长",(totolLen * 2).ToString());
        target.SetAttribute("工艺参数", "发热管槽数量", (2 * heaterLines.Count).ToString());

        List<Body> tubes = NXFunction.GetBodiesByName("TUBE-");
        tubes.MoveBodies2Layer(40);
    }

    private double GetSegments()
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        double result = 0;

        NXObject[] objects1 = workPart.Layers.GetAllObjectsOnLayer(layerNum);
        
        for (int i = 0; i < objects1.Length; i++)
        {
            HeaterSegment segment = new HeaterSegment();

            segment.type = objects1[i].IsCurve();
            if (segment.type > 3) continue;
            
            segment.curve = (Curve)objects1[i];
            
            if (segment.curve == null) continue;
            result += segment.curve.GetLength();

            if (segment.type == 1)//Line
            {
                Line line = (Line)segment.curve;
                if (line == null) continue;
                segment.p1 = line.StartPoint;
                segment.p2 = line.EndPoint;
                segment.added = false;
            }
            else if (segment.type == 3)//Spline
            {
                Spline spline1 = (Spline)segment.curve;
                if (spline1 == null) continue;
                Point4d[] ps = spline1.GetPoles();
                if (ps.Length < 2) continue;

                double w1 = ps[0].W;
                Point3d p1 = new Point3d(ps[0].X / w1, ps[0].Y / w1, 0);

                double w2 = ps[ps.Length - 1].W;
                Point3d p2 = new Point3d(ps[ps.Length - 1].X / w2, ps[ps.Length - 1].Y / w2, 0);

                segment.p1 = p1;
                segment.p2 = p2;
                segment.added = false;
            }
		    else if(segment.type == 2)//Arc
		    {
			    Arc arc1 = (Arc)segment.curve;
                if (arc1==null) continue;
			    Point3d o = arc1.CenterPoint;
                double r = arc1.Radius;
                double s = arc1.StartAngle;
                double e = arc1.EndAngle;

                segment.p1.X = o.X + r * Math.Cos(s);
                segment.p1.Y = o.Y + r * Math.Sin(s);
                segment.p1.Z = 0;
			    segment.p2.X = o.X + r * Math.Cos(e);
                segment.p2.Y = o.Y + r * Math.Sin(e);
                segment.p2.Z = 0;
			    segment.added = false;
		    }

            segments.Add(segment);
	    }

	    return result;
    }

    /// <summary>
    /// 根据一条直线，从两端搜索，获得一条完整的发热管中心线
    /// </summary>
    /// <param name="aline"></param>
    /// <returns></returns>
    private HeaterLine GetHeaterLine(Curve aline)
    {
        Line line = (Line)aline;

        HeaterLine heater_line = new HeaterLine();
        heater_line.length = line.GetLength();
        heater_line.start_point = line.StartPoint;
        heater_line.end_point = line.EndPoint;
        heater_line.orderPoint = new Point3d(0,0,0);        

        heater_line.curves.Add(aline);
        Vector3d dir = new Vector3d(heater_line.start_point.X - heater_line.end_point.X,
                    heater_line.start_point.Y - heater_line.end_point.Y, 0);
        heater_line.dir = dir;

        #region 1——从end_point找
        bool is_finish = false;
        while (is_finish == false)
        {
            is_finish = true;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].added == true) continue;

                if (!NXFunction.IsCoinCide(segments[i].p1, heater_line.end_point) &&
                   !NXFunction.IsCoinCide(segments[i].p2, heater_line.end_point))
                    continue;

                is_finish = false;
                segments[i].added = true;

                heater_line.curves.Add(segments[i].curve);
                heater_line.length += segments[i].curve.GetLength();

                heater_line.orderPoint.X += segments[i].p1.X;
                heater_line.orderPoint.X += segments[i].p2.X;
                heater_line.orderPoint.Y += segments[i].p1.Y;
                heater_line.orderPoint.Y += segments[i].p2.Y;

                #region 端点传递和方向
                if (NXFunction.IsCoinCide(segments[i].p1, heater_line.end_point))
                {
                    heater_line.end_point = segments[i].p2;
                    heater_line.other_point = segments[i].p1;
                    heater_line.other = 1;

                    Vector3d dir1 = new Vector3d(segments[i].p1.X - segments[i].p2.X,
                                 segments[i].p1.Y - segments[i].p2.Y, 0);
                    heater_line.dir = dir1;
                }
			    else
			    {
                    heater_line.end_point = segments[i].p1;
                    heater_line.other_point = segments[i].p2;
                    heater_line.other = 1;

                    Vector3d dir1 = new Vector3d(segments[i].p2.X - segments[i].p1.X,
                                    segments[i].p2.Y - segments[i].p1.Y, 0);
                    heater_line.dir = dir1;
                }
                #endregion
            }
        }
        #endregion

        #region 2——从start_point找
        is_finish = false;		
	    while(is_finish==false)
	    {
		    is_finish = true;
		    for(int i = 0; i< segments.Count;i++)
		    {
			    if(segments[i].added == true) continue;

			    if(!NXFunction.IsCoinCide(segments[i].p1,heater_line.start_point)&&
			       !NXFunction.IsCoinCide(segments[i].p2,heater_line.start_point) )
			       continue;

			    is_finish = false;
                segments[i].added = true;

			    heater_line.curves.Add(segments[i].curve);
			    heater_line.length+= segments[i].curve.GetLength();

                heater_line.orderPoint.X += segments[i].p1.X;
                heater_line.orderPoint.X += segments[i].p2.X;
                heater_line.orderPoint.Y += segments[i].p1.Y;
                heater_line.orderPoint.Y += segments[i].p2.Y;

                #region 端点传递和方向
                if (NXFunction.IsCoinCide(segments[i].p1,heater_line.start_point))
			    {
				    heater_line.start_point = segments[i].p2;
				    heater_line.other_point = segments[i].p1;
				    heater_line.other = 2;

				    Vector3d dir1 = new Vector3d(segments[i].p1.X - segments[i].p2.X,
                                 segments[i].p1.Y - segments[i].p2.Y,0);
                    heater_line.dir = dir1;
			    }
			    else
			    {
				    heater_line.start_point = segments[i].p1;
				    heater_line.other_point = segments[i].p2;
				    heater_line.other = 2;

				    Vector3d dir1 = new Vector3d(segments[i].p2.X - segments[i].p1.X,
                                 segments[i].p2.Y - segments[i].p1.Y,0);
                    heater_line.dir = dir1;
			    }
                #endregion
            }
	    }
        #endregion

        heater_line.length = Math.Round(heater_line.length, 2);//保留两位小数
        heater_line.orderPoint.X /= heater_line.curves.Count;
        heater_line.orderPoint.Y /= heater_line.curves.Count;

        int scale = 20;
        heater_line.orderPoint.X = Math.Floor(heater_line.orderPoint.X / scale) * scale;
        heater_line.orderPoint.Y = Math.Floor(heater_line.orderPoint.Y / scale) * scale;

        return heater_line;
    }

    /// <summary>
    /// 生成发热管实体 插入属性 命名 移动
    /// </summary>
    /// <param name="index"></param>
    /// <param name="heater_line"></param>
    /// <param name="outer_D"></param>
    /// <param name="innner_D"></param>
    /// <param name="Z"></param>
    private Body CreateTube(int index,HeaterLine heater_line,double outer_D,double innner_D,double Z)
    {
        NXFunction.CreateTube(index, heater_line.curves, 6.5, 0);
        Body body = NXFunction.GetBodyByName("TUBE-" + index.ToString());
        SaveCode2Tube(body, index, heater_line.length);
        body.MoveBodyByIncrement(0, 0, Z);
        return body;
    }

    /// <summary>
    /// 物料编码
    /// </summary>
    /// <param name="body"></param>
    /// <param name="curve_index"></param>
    /// <param name="length"></param>
    private void SaveCode2Tube(Body body,int curve_index, double length)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        string name_part = "TUBE-" + curve_index.ToString();
        string code = "42201";
        double error = 2;

        string strsql = "SELECT top 1 st_code as EXPR1 from (SELECT * from standard_table where SUBSTRING(st_code,1,4)='HR22' ) expr ";
        string strwhere = "where abs(CAST(SUBSTRING(st_code,10,4) as int)-" + length.ToString() + ")<=" + error.ToString();
        string str = strsql + strwhere;

        code = sql.GetExpr1AsString(str, "");             
        if (code == "")
        {
            strwhere = "where CAST(SUBSTRING(st_code,10,4) as int)>" + length.ToString() + " order by st_code";
            str = strsql + strwhere;
            code = sql.GetExpr1AsString(str, "");
        }
        if (code == "")
        {
            code = "42201";
        }


        body.SetAttribute("基本参数", "name", "M" + curve_index.ToString());
        body.SetAttribute("基本参数", "长度", length.ToString());
        body.SetAttribute("发热管", "发热管", code);
        body.SetAttribute("陶瓷接线盒1", "陶瓷接线盒1", "HR23049000001F");
        body.SetAttribute("陶瓷接线盒2", "陶瓷接线盒2", "HR23049000001F");
    }

    /// <summary>
    /// 生成上层陶瓷接线盒，并和管道合并
    /// </summary>
    /// <param name="curve_index"></param>
    /// <param name="dir"></param>
    /// <param name="heater_line"></param>
    /// <param name="Z"></param>
    private void CreateBox(Body tube,int curve_index,Vector3d dir,HeaterLine heater_line,double Z)
    {
        string path_box = sql.InstallPath + "\\Part_Base\\TBH.prt";
        heater_line.start_point.Z = Z;
        heater_line.end_point.Z = Z;

        NXFunction.ImportAndRoationY(path_box, heater_line.start_point, dir,1);
        if (NXFunction.UnitePart("TUBE-" + curve_index.ToString(), "TBH"))
            NXFunction.NumberingPart(curve_index * 2, "TBH", heater_line.start_point, 0, 1);

        NXFunction.ImportAndRoationY(path_box, heater_line.end_point, dir, 1);
        if (NXFunction.UnitePart("TUBE-" + curve_index.ToString(), "TBH"))
            NXFunction.NumberingPart(curve_index * 2, "TBH", heater_line.start_point, 0, 1);

        tube.RemoveParameters();
    }

    private Body CreateSwept(int curve_index, HeaterLine heater_line)
    {
        Point3d p1 = new Point3d(heater_line.start_point.X, heater_line.start_point.Y, -3.5);
        //插入片体获得界面
        string path_heater = sql.InstallPath + "\\Part_Base\\heater.prt";
        NXFunction.ImportAndRoationY(path_heater, p1, heater_line.dir,1);
        //扫描
        NXFunction.CreateGuideSwept(curve_index, "SHEET-HEATER", heater_line.curves, 0, "");
        NXFunction.DeleteBody("SHEET-HEATER");

        return NXFunction.GetBodyByName("SWEPT-" + curve_index.ToString());
    }

    /// <summary>
    /// 创建文字
    /// </summary>
    /// <param name="index"></param>
    /// <param name="heater_line"></param>
    private void CreateText(int index, HeaterLine heater_line)
    {
        if (heater_line.curves.Count < 2) return;

        //加热器的起点终点向量的中点
        Point3d point_mid = new Point3d((heater_line.start_point.X + heater_line.end_point.X) / 2,
                                         (heater_line.start_point.Y + heater_line.end_point.Y) / 2,
                                         0);

        //刻字插入点
        Point3d point_insert = new Point3d(0, 0, 0);
        Vector3d vertical = new Vector3d(0, 0, 0);
        if (heater_line.other == 1)
        {
            vertical.X = heater_line.other_point.X - heater_line.end_point.X;
            vertical.Y = heater_line.other_point.Y - heater_line.end_point.Y;
        }
        else
        {
            vertical.X = heater_line.other_point.X - heater_line.start_point.X;
            vertical.Y = heater_line.other_point.Y - heater_line.start_point.Y;
        }
        double unitization = Math.Sqrt(vertical.X * vertical.X + vertical.Y * vertical.Y);
        vertical.X = vertical.X / unitization;
        vertical.Y = vertical.Y / unitization;
        point_insert.X = 32 * vertical.X + point_mid.X;
        point_insert.Y = 32 * vertical.Y + point_mid.Y;

        //文字的向量
        Vector3d vec_txt = new Vector3d(heater_line.end_point.X - heater_line.start_point.X,
                        heater_line.end_point.Y - heater_line.start_point.Y, 0);
        double verify = (heater_line.start_point.X - point_insert.X) * (heater_line.end_point.Y - point_insert.Y);
        verify = verify - (heater_line.start_point.Y - point_insert.Y) * (heater_line.end_point.X - point_insert.X);

        vec_txt.X *= verify;
        vec_txt.Y *= verify;

        //创建文字
        NXFunction.CreateText(point_insert, 1, vec_txt, "M" + index.ToString());
        point_insert.Z = -manifold.ManifoldH;
        NXFunction.CreateText(point_insert, -1, vec_txt, "M" + index.ToString());
    }

    /// <summary>
    /// 刻字
    /// </summary>
    private void Crave()
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        List<Text> texts = new List<Text>();
        
        FeatureCollection cols = workPart.Features;
        foreach (Feature feature in cols)
        {
            string featureType = feature.FeatureType.ToString();
            if (featureType != "TEXT") continue;

            Text text = (Text)feature;
            if (text == null) continue;

            texts.Add(text);
        }
        
        NXFunction.CraveOnManifold(texts);
        NXFunction.RemoveParameters("MANIFOLD");

        //删除发热管文字
        Session.UndoMarkId markId1;
        markId1 = theSession.SetUndoMark(Session.MarkVisibility.Invisible, "Delete");
        bool notifyOnDelete1 = theSession.Preferences.Modeling.NotifyOnDelete;

        theSession.UpdateManager.ClearErrorList();
        Session.UndoMarkId markId2;
        markId2 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Delete");

        NXObject[] objs = new NXObject[texts.Count];
        for (int i = 0; i<texts.Count; i++)
            objs[i] = texts[i];
        int nErrs1 = theSession.UpdateManager.AddToDeleteList(objs);

        bool notifyOnDelete2 = theSession.Preferences.Modeling.NotifyOnDelete;
        int nErrs2 = theSession.UpdateManager.DoUpdate(markId2);
        theSession.DeleteUndoMark(markId1, null);
    }
}

class HeaterSegment
{
	public Curve curve;
    public Point3d p1;
    public Point3d p2;
    public bool added;
    //public int data;//1——spline 2——arc圆弧 3——line  4——arc整圆
    public int type;
    public Vector3d dir;

    public HeaterSegment()
    { }
};

class HeaterLine//一条完整的加热线中心线
{
	public List<Curve> curves = new List<Curve>();
    public Point3d start_point;
    public Point3d end_point;
    public Point3d other_point;
    public Point3d orderPoint;
    public int other;//1-end point,2-start point
    public double length;
    public Vector3d dir;
};

class Heater
{
}
