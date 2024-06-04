using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Windows; 
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using FormProgressBar;
using MoveUOTable;

[assembly: AssemblyVersion("1.0.0.25")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public void Execute(ScriptContext context)
        {
            try
            {
                if (context.StructureSet == null)
                {
                    MessageBox.Show("Please open a structure set before starting the script");
                }
                else
                {
                    context.Patient.BeginModifications();

                    if (context.StructureSet.Structures.First().HasCalculatedPlans)
                    {
                        MessageBox.Show("The chosen structure set is linked to one or several treatment plans with calculated dose. Cannot move User Origin or add a support structure");
                        return;
                    }
                    else
                    {
                        bool CheckIfCouchExist = IsCouch(context);
                        if (!CheckIfCouchExist)
                        {
                            MoveUOTable.UserInterface UI = new MoveUOTable.UserInterface();
                            if (UI.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                CouchType couchType = UI.CouchThickness;
                                ProgressBar ProgressBarPopUp = new ProgressBar(context.Image);
                                ProgressBarPopUp.Show();

                                AddCouchStructures(context.StructureSet, ProgressBarPopUp, couchType);

                                ProgressBarPopUp.ChangeLabel("Moving User Origin to right position");
                                //MoveUserOrigin(context.StructureSet); // user origin måste flyttas till markör, fixas senare
                                ProgressBarPopUp.ChangeProgressBarValue();
                                ProgressBarPopUp.Close();

                                MessageBox.Show("Done");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error" + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
            }
        }

        private static bool IsCouch(ScriptContext context)
        {
            bool CheckIfCouchExist = false;
            if (context.StructureSet.Structures.Where(s => s.Id.Equals("CouchInterior")).FirstOrDefault() == null && context.StructureSet.Structures.Where(s => s.Id.Equals("CouchSurface")).FirstOrDefault() == null)
            {
                CheckIfCouchExist = false;
            }
            else
            {
                CheckIfCouchExist = true;
                MessageBox.Show("Couch structures already exists");
            }

            return CheckIfCouchExist;
        }

        private static void AddCouchStructures(StructureSet structureSet, ProgressBar ProgressBarPopUp, CouchType CouchThickness)
        {
            if (structureSet.Structures.Where(s => s.Id.Equals("CouchInterior")).FirstOrDefault() == null && structureSet.Structures.Where(s => s.Id.Equals("CouchSurface")).FirstOrDefault() == null)
            {
                double couchEdgeOffset = GetCouchEdgeOffset(CouchThickness);

                double[] UOtoCenterPoint_Offset = GetOffset(structureSet.Image, couchEdgeOffset);
                structureSet.AddCouchStructures(CouchThickness.ToString(), structureSet.Image.ImagingOrientation, RailPosition.In, RailPosition.In, null, null, null, out IReadOnlyList<Structure> List, out bool out1, out string out2);
                MoveCouchToRightPosition(structureSet, UOtoCenterPoint_Offset[0], UOtoCenterPoint_Offset[1], ProgressBarPopUp);
            }
            else
            {
                MessageBox.Show("Couch structures already exists");
            }
        }

        private static double GetCouchEdgeOffset(CouchType CouchThickness)
        {
            double couchEdgeOffset = 0;
            string filename = @"\\ltvastmanland.se\ltv\shares\rhosonk\Strålbehandling\VARIAN\Eclipse Script\Offset.txt";
            string[] lines = System.IO.File.ReadAllLines(filename);
            switch (CouchThickness)
            {
                case CouchType.Exact_IGRT_Couch_Top_thick:
                    couchEdgeOffset = double.Parse(lines[(int)CouchType.Exact_IGRT_Couch_Top_thick]);
                    break;
                case CouchType.Exact_IGRT_Couch_Top_medium:
                    couchEdgeOffset = double.Parse(lines[(int)CouchType.Exact_IGRT_Couch_Top_medium]);
                    break;
                case CouchType.Exact_IGRT_Couch_Top_thin:
                    couchEdgeOffset = double.Parse(lines[(int)CouchType.Exact_IGRT_Couch_Top_thin]);
                    break;
            }
            
            return couchEdgeOffset;
        }

        //Erik Furas tillägg baserat på Linnea Lunds kod
        #region GetOffset
        private static double[] GetOffset(Image bild, double couchEdgeOffset)
        {
            int[,] matris = GetMatris(bild.UserOrigin.z, bild);

            int pixelBordskant = HittaBordskant(matris, bild, 0); //Hittar först bordskanten med antagandet att user origin är placerat i mitten av bordet i x-led
            int xPixelOffset = GetXpixelOffset(matris, bild, pixelBordskant); //kontrollerar om user origin är placerat i mitten m.h.a. nyfunna bordskanten
            pixelBordskant = HittaBordskant(matris, bild, xPixelOffset); //korrigerar för eventuell lateral diff
            double yKoord = yPixeltoCoordinate(pixelBordskant, bild) - couchEdgeOffset; 

            int UserOriginXpixel = Convert.ToInt32(Math.Abs((bild.UserOrigin.x - bild.Origin.x) / bild.XRes));
            double xKoord = xPixeltoCoordinate(UserOriginXpixel + xPixelOffset, bild);

            double[] coords = new double[2] { yKoord, xKoord };
            return coords;
        }
        private static int[,] GetMatris(double snitt, Image bild)
        {
            int[,] matris = new int[bild.XSize, bild.YSize];
            int bord_botten_snitt = Convert.ToInt32(Math.Abs((bild.UserOrigin.z - bild.Origin.z) / bild.ZRes));
            bild.GetVoxels(bord_botten_snitt, matris);
            return matris;
        }
        private static int HittaBordskant(int[,] matris, Image bild, int xPixelOffset)
        {
            int letandeStart = bild.YSize; //börjar leta efter kanten från första pixeln posteriort sett
            int UserOriginXpixel = Convert.ToInt32(Math.Abs((bild.UserOrigin.x - bild.Origin.x) / bild.XRes)) + xPixelOffset;
            string resultat = "";
            int i = 1;
            double HU = -1000;
            int yPixel = 0;
            bool hittatBordskant = false;
            while (!hittatBordskant)
            {
                HU = bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - i]);
                if (HU > -200)
                {
                    HU = bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - i - 1]);
                    if (HU < -200)
                    {
                        for (int j = 1; j < Math.Round(55 / bild.YRes); j++)
                        {
                            HU = bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - j - i]);
                            if (HU > -200)
                            {
                                HU = bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - j - i - 1]);
                                if (HU < -200)
                                {
                                    hittatBordskant = true;
                                    yPixel = letandeStart - j - i - 1;
                                }
                            }
                        }
                        if (!hittatBordskant)
                        {
                            hittatBordskant = true;
                            yPixel = letandeStart - i;
                        }
                    }
                }
                resultat = resultat + "\n" + "i = " + i.ToString() + ", HU = " + bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - i]) + ", HU-kvot: " + bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - i - 1]) / bild.VoxelToDisplayValue(matris[UserOriginXpixel, letandeStart - i]);
                i++;
            }

            return yPixel;
        }
        static int GetXpixelOffset(int[,] matris, Image bild, int yPixelBordskant)
        {
            int UserOriginXpixel = Convert.ToInt32(Math.Abs((bild.UserOrigin.x - bild.Origin.x) / bild.XRes));
            double HU = -1000;
            int i = 0;
            int j = 0;
            int yPixelOffset = -6;
            int xKoordPos = UserOriginXpixel;
            while (HU < -800)
            {
                i++;
                xKoordPos = UserOriginXpixel + i;
                HU = bild.VoxelToDisplayValue(matris[xKoordPos, yPixelBordskant + yPixelOffset]); // en offset i y för att kolla hur långt bort sidorna är i båda x-led
            }
            int xKoordNeg = UserOriginXpixel;
            HU = -1000;
            while (HU < -800)
            {
                j--;
                xKoordNeg = UserOriginXpixel + j;
                HU = bild.VoxelToDisplayValue(matris[xKoordNeg, yPixelBordskant + yPixelOffset]);
            }
            int xKoord;
            int xDiff = (xKoordPos - UserOriginXpixel + xKoordNeg - UserOriginXpixel) / 2;
            if (Math.Abs(xDiff) < 10) // Om laterala skillnaden är mindre än 10 pixlar är det OK
                xKoord = 0;
            else
                xKoord = xDiff;

            return xKoord;
        }
        private static double yPixeltoCoordinate(double pixel, Image bild)
        {
            double yCoord = bild.Origin.y + bild.YRes * pixel;
            return yCoord;
        }
        private static double xPixeltoCoordinate(double pixel, Image bild)
        {
            double xCoord = bild.Origin.x + bild.XRes * pixel;
            return xCoord;
        }

        #endregion 

        private static void MoveCouchToRightPosition(StructureSet structureSet, double UOtoCenterPoint_Offsety, double UOtoCenterPoint_Offsetx, ProgressBar ProgressBarPopUp)
        {
            Structure CouchSurface = structureSet.Structures.First(x => x.Id.Equals("CouchSurface"));
            Structure CouchInterior = structureSet.Structures.First(x => x.Id.Equals("CouchInterior"));

            double offsety = -CouchSurface.CenterPoint.y + UOtoCenterPoint_Offsety; //Calculate how much the couch should be moved vertically to be in the right position
            double offsetx = -CouchSurface.CenterPoint.x + UOtoCenterPoint_Offsetx; //Calculate how much the couch should be moved vertically to be in the right position

            ProgressBarPopUp.ChangeLabel("Moving couch structures in slice 1/" + structureSet.Image.ZSize);
            MoveCouchStructureInSlice(CouchSurface, 0, offsety, offsetx);
            MoveCouchStructureInSlice(CouchInterior, 0, offsety, offsetx);
            ProgressBarPopUp.ChangeProgressBarValue();

            VVector[] ContourSurface_Contour = CouchSurface.GetContoursOnImagePlane(0)[0];
            VVector[] ContourInterior_Contour = CouchInterior.GetContoursOnImagePlane(0)[0];

                for (int i = 1; i < structureSet.Image.ZSize; i++) //The couch contours only need to be moved to the right position in the first slice. The contours can then be copied to the remaining slices. Small efficiency/time gain?
            {
                ProgressBarPopUp.ChangeLabel("Moving couch structures in slice " + (i + 1).ToString() + "/" + structureSet.Image.ZSize);

                CouchSurface.ClearAllContoursOnImagePlane(i);
                CouchSurface.AddContourOnImagePlane(ContourSurface_Contour, i);

                CouchInterior.ClearAllContoursOnImagePlane(i);
                CouchInterior.AddContourOnImagePlane(ContourInterior_Contour, i);
                
                ProgressBarPopUp.ChangeProgressBarValue();
            }
            CouchSurface.SegmentVolume = CouchSurface.Xor(CouchInterior.SegmentVolume);
        }


        private static void MoveCouchStructureInSlice(Structure structure, int SliceIndex, double offsety, double offsetx)
        {
            var OriginalContours = structure.GetContoursOnImagePlane(SliceIndex);
            structure.ClearAllContoursOnImagePlane(SliceIndex);

            int k = 0;

            VVector[] newcontour = OriginalContours[0];


            foreach (var pt in OriginalContours[0])
            {
                var coordx = pt.x;
                var coordy = pt.y;
                var coordz = pt.z;
                newcontour[k] = new VVector(coordx + offsetx, coordy + offsety, coordz);
                k++;
            }
            structure.AddContourOnImagePlane(newcontour, SliceIndex);
        }
    }
}
