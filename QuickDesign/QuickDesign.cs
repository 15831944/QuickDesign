using System;
using NXOpen;
using NXOpen.UF;
using NXOpen.BlockStyler;

public class QuickDesign
{
    #region 控件
    private static Session theSession = null;
    private static UI theUI = null;
    private string theDlxFileName;
    private NXOpen.BlockStyler.BlockDialog theDialog;
    private NXOpen.BlockStyler.Group group;// Block type: Group
    private NXOpen.BlockStyler.StringBlock sSR;// Block type: String
    private NXOpen.BlockStyler.Enumeration eZF;// Block type: Enumeration
    private NXOpen.BlockStyler.DoubleBlock dMH;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock dRunnerD;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock dOffset;// Block type: Double
    private NXOpen.BlockStyler.StringBlock sTin;// Block type: String
    private NXOpen.BlockStyler.StringBlock sBin;// Block type: String
    private NXOpen.BlockStyler.Group group1;// Block type: Group
    private NXOpen.BlockStyler.DoubleBlock dCH;// Block type: Double
    #endregion

    DataControl sql = new DataControl();

    public QuickDesign()
    {
        try
        {
            theSession = Session.GetSession();
            theUI = UI.GetUI();
            theDlxFileName = "QuickDesign.dlx";
            theDialog = theUI.CreateDialog(theDlxFileName);
            theDialog.AddApplyHandler(new NXOpen.BlockStyler.BlockDialog.Apply(apply_cb));
            theDialog.AddOkHandler(new NXOpen.BlockStyler.BlockDialog.Ok(ok_cb));
            theDialog.AddUpdateHandler(new NXOpen.BlockStyler.BlockDialog.Update(update_cb));
            theDialog.AddCancelHandler(new NXOpen.BlockStyler.BlockDialog.Cancel(cancel_cb));
            theDialog.AddInitializeHandler(new NXOpen.BlockStyler.BlockDialog.Initialize(initialize_cb));
            theDialog.AddFocusNotifyHandler(new NXOpen.BlockStyler.BlockDialog.FocusNotify(focusNotify_cb));
            theDialog.AddDialogShownHandler(new NXOpen.BlockStyler.BlockDialog.DialogShown(dialogShown_cb));
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            throw ex;
        }
    }
    
    public static void Main()
    {
        QuickDesign theQuickDesign = null;
        try
        {
            theQuickDesign = new QuickDesign();
            // The following method shows the dialog immediately
            theQuickDesign.Show();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        finally
        {
            if(theQuickDesign != null)
                theQuickDesign.Dispose();
                theQuickDesign = null;
        }
    }
    
    public static int GetUnloadOption(string arg)
    {
        //return System.Convert.ToInt32(Session.LibraryUnloadOption.Explicitly);
         return System.Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        // return System.Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
    }
        
    public static void UnloadLibrary(string arg)
    {
        try
        {
            //---- Enter your code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }
        
    public NXOpen.UIStyler.DialogResponse Show()
    {
        try
        {
            theDialog.Show();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return 0;
    }
        
    public void Dispose()
    {
        if(theDialog != null)
        {
            theDialog.Dispose();
            theDialog = null;
        }
    }
        
    public void initialize_cb()
    {
        try
        {
            group = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group");
            sSR = (NXOpen.BlockStyler.StringBlock)theDialog.TopBlock.FindBlock("sSR");
            eZF = (NXOpen.BlockStyler.Enumeration)theDialog.TopBlock.FindBlock("eZF");
            dMH = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("dMH");
            dRunnerD = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("dRunnerD");
            dOffset = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("dOffset");
            sTin = (NXOpen.BlockStyler.StringBlock)theDialog.TopBlock.FindBlock("sTin");
            sBin = (NXOpen.BlockStyler.StringBlock)theDialog.TopBlock.FindBlock("sBin");
            group1 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group1");
            dCH = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("dCH");
        }
        catch (Exception ex)
        {
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }
        
    public void dialogShown_cb()
    {
        try
        {
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }
        
    public int apply_cb()
    {
        int errorCode = 0;
        try
        {
            DataControl sql = new DataControl();

            ManifoldInfo manifold = GetUserInput();

            //ManifoldBuilder builder = new ManifoldBuilder(manifold);
            //builder.Commit();

            //HeaterBuilder heaterBuilder = new HeaterBuilder(manifold);
            //heaterBuilder.Commit();

            new InletBushingBuilder(sql,manifold).Commit();
            new CentrePinBuilder(sql, manifold).Commit();
            new DowelPinBuilder(sql, manifold).Commit();
            new TCBuilder(sql, manifold).Commit();
            new InsulatorBuilder(sql, manifold).Commit();

            //RunnerInsertBuilder runnerInsertBuilder = new RunnerInsertBuilder(manifold);
            //runnerInsertBuilder.Commit();

            //GeneralPartBuilder generalPartBuilder = new GeneralPartBuilder();
            //generalPartBuilder.Commit("SWP",0);
            //generalPartBuilder.Commit("OIL", -manifold.ManifoldH);
            //generalPartBuilder.Commit("OMS-CB", -manifold.ManifoldH);
            //generalPartBuilder.Commit("OMF-CB", manifold.TopH + manifold.CylinderH - 25);
            //generalPartBuilder.Commit("AIR", manifold.TopH + manifold.CylinderH - 25);
            //generalPartBuilder.Commit("ADP", -manifold.ManifoldH);
            //generalPartBuilder.Commit("ZJU", -manifold.ManifoldH);
            //generalPartBuilder.Commit("ZJO", 0);

            //generalPartBuilder.CommitOther();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            errorCode = 1;
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return errorCode;
    }

    private ManifoldInfo GetUserInput()
    {
        ManifoldInfo manifold = new ManifoldInfo();

        manifold.ManifoldH = dMH.Value;
        manifold.RunnerDiameter = dRunnerD.Value;
        manifold.RunnerOffset = dOffset.Value;
        manifold.CylinderH = dCH.Value;
        manifold.TopH = double.Parse(sTin.Value);
        manifold.BottomH = double.Parse(sBin.Value);
        manifold.InstallType = eZF.ValueAsString;//文字自动转为0,1,2
        manifold.SR = double.Parse(sSR.Value);

        manifold.SetSeriesNum(sql.GetSeries());
        manifold.Type = NXFunction.XiTong;

        return manifold;
    }

    public int update_cb( NXOpen.BlockStyler.UIBlock block)
    {
        try
        {
            if(block == sSR)
            {
            //---------Enter your code here-----------
            }
            else if(block == eZF)
            {
            //---------Enter your code here-----------
            }
            else if(block == dMH)
            {
            //---------Enter your code here-----------
            }
            else if(block == dRunnerD)
            {
            //---------Enter your code here-----------
            }
            else if(block == dOffset)
            {
            //---------Enter your code here-----------
            }
            else if(block == sTin)
            {
            //---------Enter your code here-----------
            }
            else if(block == sBin)
            {
            //---------Enter your code here-----------
            }
            else if(block == dCH)
            {
            //---------Enter your code here-----------
            }
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return 0;
    }
        
    public int ok_cb()
    {
        int errorCode = 0;
        try
        {
            errorCode = apply_cb();
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            errorCode = 1;
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return errorCode;
    }
        
    public int cancel_cb()
    {
        try
        {
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return 0;
    }
        
    public void focusNotify_cb(NXOpen.BlockStyler.UIBlock block, bool focus)
    {
        try
        {
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }
        
    public PropertyList GetBlockProperties(string blockID)
    {
        PropertyList plist =null;
        try
        {
            plist = theDialog.GetBlockProperties(blockID);
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return plist;
    }
    
}
