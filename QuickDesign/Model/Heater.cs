using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NXOpen;
using NXOpen.UF;

class HeaterBuilder
{
    ManifoldInfo manifold;
    DataControl sql;
    int layerNum;

    List<HeaterSegment> segments;//所有加热线

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

    //根据一条直线，从两端搜索，获得一条完整的发热管中心线
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
        
	    return heater_line;
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
	public List<Curve> curves;
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
