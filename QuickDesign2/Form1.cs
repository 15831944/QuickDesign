using ExpertLib.NX.BHRT;
using NXOpen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace QuickDesign2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DataControl sql = new DataControl();
            ManifoldInfo manifold = new ManifoldInfo();

            manifold.ManifoldH = 55;
            manifold.RunnerDiameter = 12;
            manifold.RunnerOffset = 0;
            manifold.CylinderH = 80;
            manifold.TopH = 10;
            manifold.BottomH = 10;
            manifold.InstallType = "整体式";//文字自动转为0,1,2
            manifold.SR =21;

            manifold.SetSeriesNum(sql.GetSeries());
            manifold.Type = NXFunction.XiTong;

            NXFunction.SetLayers();

            ManifoldBuilder builder = new ManifoldBuilder(manifold);
            builder.Submit();
            ManifoldBoltBuilder boltBuilder = new ManifoldBoltBuilder(manifold);
            boltBuilder.Submit();

            HeaterBuilder heaterBuilder = new HeaterBuilder(manifold);
            heaterBuilder.Submit();

            new InletBushingBuilder(sql, manifold).Submit();
            new CentrePinBuilder(sql, manifold).Submit();
            new DowelPinBuilder(sql, manifold).Submit();
            new TCBuilder(sql, manifold).Submit();
            new InsulatorBuilder(sql, manifold).Submit();

            RunnerInsertBuilder runnerInsertBuilder = new RunnerInsertBuilder(manifold);
            runnerInsertBuilder.Submit();

            GeneralPartBuilder generalPartBuilder = new GeneralPartBuilder(manifold);
            generalPartBuilder.Submit();

            //气缸油缸
            NozzleCylinderBuilder cylinderBuilder = new NozzleCylinderBuilder(manifold, false);
            cylinderBuilder.Submit();

            //流道
            builder.CreateRunner();

            //线架
            WireFrameBuilder wireFrame = new WireFrameBuilder(manifold);
            wireFrame.Submit();

            //隐藏假体
            List<Body> bodies = NXFunction.GetBodiesByName("SUB");
            bodies.MoveBodies2Layer(42);
        }
    }
}
