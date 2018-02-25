using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class ShadowCollisionTool
{
    public static void Main()
    {
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

        string[] Arguments = Environment.GetCommandLineArgs();

        Console.WriteLine("============================================");
        Console.WriteLine("| Shadow Collision Tool by igorseabra4");
        Console.WriteLine("| Usage: drag .OBJ model files into the executable to convert them to Shadow the Hedgehog collision .BSP.");
        Console.WriteLine("| Just opening the program will convert every file found in the folder.");
        Console.WriteLine("| Dragging Shadow .BSP into the program will convert those to .OBJ (you have to drag those).");
        Console.WriteLine("============================================");

        if (Arguments.Length <= 1)
            Arguments = Directory.GetFiles(Directory.GetCurrentDirectory());

        foreach (string i in Arguments)
            if (Path.GetExtension(i).ToLower() == ".obj")
                ConvertOBJtoBSP(i);
            else if (Path.GetExtension(i).ToLower() == ".bsp")
                ConvertBSPtoOBJ(i);
        Console.ReadKey();
    }
    
    public class Vertex
    {
        public float X;
        public float Y;
        public float Z;

        public Vertex(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class Triangle
    {
        public uint MaterialIndex;

        public uint Vertex1;
        public uint Vertex2;
        public uint Vertex3;

        public Triangle(uint v1, uint v2, uint v3, uint v4)
        {
            Vertex1 = v1;
            Vertex2 = v2;
            Vertex3 = v3;
            MaterialIndex = v4;
        }
    }
    
    public class TreeNode
    {
        public byte Type;
        public byte TriangleAmount;
        public ushort ChildIndex;
        public ushort TriangleReferenceIndex;
        public float SplitPosition;
        public bool isLeaf;

        public float MinX;
        public float MinY;
        public float MinZ;
        public float MaxX;
        public float MaxY;
        public float MaxZ;

        public List<ushort> TriangleIndexList;
    }

    public class ColSplit
    {
        public TreeNode LeftNode;
        public TreeNode RightNode;
    }

    static int AmountOfMeshes = 0;
    static List<Vertex> VertexList = new List<Vertex>();
    static List<Triangle> TriangleList = new List<Triangle>();
    static List<ColSplit> SplitList = new List<ColSplit>();
    static List<ushort> TriangleIndexReferenceList = new List<ushort>();
    
    public static void ConvertOBJtoBSP(string FileName)
    {
        VertexList.Clear();
        TriangleList.Clear();
        SplitList.Clear();
        TriangleIndexReferenceList.Clear();
        AmountOfMeshes = 0;

        Console.WriteLine("Reading " + FileName);
        ReadOBJFile(FileName);
        GenerateBinaryTree();
        CreateBSPFile(FileName);
        Console.WriteLine("Success.");
    }

    public static void ReadOBJFile(string InputFile)
    {
        string[] OBJFile = File.ReadAllLines(InputFile);
        
        foreach (string j in OBJFile)
        {
            if (j.Length > 2)
            {
                if (j.Substring(0, 2) == "v ")
                {
                    string[] a = Regex.Replace(j, @"\s+", " ").Split(' ');
                    VertexList.Add(new Vertex(Convert.ToSingle(a[1]), Convert.ToSingle(a[2]), Convert.ToSingle(a[3])));
                }
                else if (j.StartsWith("f "))
                {
                    string[] SubStrings = j.Split(' ');                    
                    TriangleList.Add(new Triangle(
                        Convert.ToUInt32(SubStrings[1].Split('/')[0]) - 1,
                        Convert.ToUInt32(SubStrings[2].Split('/')[0]) - 1,
                        Convert.ToUInt32(SubStrings[3].Split('/')[0]) - 1,
                        (uint)AmountOfMeshes-1));
                }
                else if (j.StartsWith("g ") | j.StartsWith("o "))
                    AmountOfMeshes++;
            }
        }
    }
    
    public static void GenerateBinaryTree()
    {
        // Get maximum bounding box
        float MaxX = VertexList[0].X;
        float MaxY = VertexList[0].Y;
        float MaxZ = VertexList[0].Z;
        float MinX = VertexList[0].X;
        float MinY = VertexList[0].Y;
        float MinZ = VertexList[0].Z;

        foreach (Vertex i in VertexList)
        {
            if (i.X > MaxX)
                MaxX = i.X;
            if (i.Y > MaxY)
                MaxY = i.Y;
            if (i.Z > MaxZ)
                MaxZ = i.Z;
            if (i.X < MinX)
                MinX = i.X;
            if (i.Y < MinY)
                MinY = i.Y;
            if (i.Z < MinZ)
                MinZ = i.Z;
        }

        TreeNode treeNode = new TreeNode
        {
            MaxX = MaxX,
            MaxY = MaxY,
            MaxZ = MaxZ,
            MinX = MinX,
            MinY = MinY,
            MinZ = MinZ,
            TriangleIndexList = new List<ushort>(TriangleList.Count)
        };
        for (ushort i = 0; i < TriangleList.Count; i++)
            treeNode.TriangleIndexList.Add(i);
        
        SplitTreeNode(treeNode, 20, 0);
    }

    static ushort PositionOnList = 0;

    public static void SplitTreeNode(TreeNode t, int MaxTrianglesOnNode, int recursion)
    {
        float XDist = t.MaxX - t.MinX;
        float YDist = t.MaxY - t.MinY;
        float ZDist = t.MaxZ - t.MinZ;

        if (XDist > YDist & XDist > ZDist)
        {
            ColSplit newSplitX = new ColSplit
            {
                LeftNode = new TreeNode(),
                RightNode = new TreeNode()
            };

            newSplitX.LeftNode.TriangleIndexList = new List<ushort>();
            newSplitX.LeftNode.SplitPosition = (t.MaxX + t.MinX) / 2;
            newSplitX.LeftNode.MinX = t.MinX;
            newSplitX.LeftNode.MinY = t.MinY;
            newSplitX.LeftNode.MinZ = t.MinZ;
            newSplitX.LeftNode.MaxX = newSplitX.LeftNode.SplitPosition;
            newSplitX.LeftNode.MaxY = t.MaxY;
            newSplitX.LeftNode.MaxZ = t.MaxZ;
            newSplitX.LeftNode.Type = 1;

            newSplitX.RightNode.TriangleIndexList = new List<ushort>();
            newSplitX.RightNode.SplitPosition = (t.MaxX + t.MinX) / 2;
            newSplitX.RightNode.MinX = newSplitX.RightNode.SplitPosition;
            newSplitX.RightNode.MinY = t.MinY;
            newSplitX.RightNode.MinZ = t.MinZ;
            newSplitX.RightNode.MaxX = t.MaxX;
            newSplitX.RightNode.MaxY = t.MaxY;
            newSplitX.RightNode.MaxZ = t.MaxZ;
            newSplitX.RightNode.Type = 0;

            newSplitX.LeftNode = FindTrianglesInsideNode(newSplitX.LeftNode);
            newSplitX.RightNode = FindTrianglesInsideNode(newSplitX.RightNode);

            SplitList.Add(newSplitX);
        }
        else if (YDist > ZDist)
        {
            ColSplit newSplitY = new ColSplit
            {
                LeftNode = new TreeNode(),
                RightNode = new TreeNode()
            };
            newSplitY.LeftNode.TriangleIndexList = new List<ushort>();
            newSplitY.LeftNode.SplitPosition = (t.MaxY + t.MinY) / 2;
            newSplitY.LeftNode.MinX = t.MinX;
            newSplitY.LeftNode.MinY = t.MinY;
            newSplitY.LeftNode.MinZ = t.MinZ;
            newSplitY.LeftNode.MaxX = t.MaxX;
            newSplitY.LeftNode.MaxY = newSplitY.LeftNode.SplitPosition;
            newSplitY.LeftNode.MaxZ = t.MaxZ;
            newSplitY.LeftNode.Type = 5;

            newSplitY.RightNode.TriangleIndexList = new List<ushort>();
            newSplitY.RightNode.SplitPosition = (t.MaxY + t.MinY) / 2;
            newSplitY.RightNode.MinX = t.MinX;
            newSplitY.RightNode.MinY = newSplitY.RightNode.SplitPosition;
            newSplitY.RightNode.MinZ = t.MinZ;
            newSplitY.RightNode.MaxX = t.MaxX;
            newSplitY.RightNode.MaxY = t.MaxY;
            newSplitY.RightNode.MaxZ = t.MaxZ;
            newSplitY.RightNode.Type = 4;

            newSplitY.LeftNode = FindTrianglesInsideNode(newSplitY.LeftNode);
            newSplitY.RightNode = FindTrianglesInsideNode(newSplitY.RightNode);

            SplitList.Add(newSplitY);
        }
        else
        {
            ColSplit newSplitZ = new ColSplit
            {
                LeftNode = new TreeNode(),
                RightNode = new TreeNode()
            };
            newSplitZ.LeftNode.TriangleIndexList = new List<ushort>();
            newSplitZ.LeftNode.SplitPosition = (t.MaxZ + t.MinZ) / 2;
            newSplitZ.LeftNode.MinX = t.MinX;
            newSplitZ.LeftNode.MinY = t.MinY;
            newSplitZ.LeftNode.MinZ = t.MinZ;
            newSplitZ.LeftNode.MaxX = t.MaxX;
            newSplitZ.LeftNode.MaxY = t.MaxY;
            newSplitZ.LeftNode.MaxZ = newSplitZ.LeftNode.SplitPosition;
            newSplitZ.LeftNode.Type = 9;

            newSplitZ.RightNode.TriangleIndexList = new List<ushort>();
            newSplitZ.RightNode.SplitPosition = (t.MaxZ + t.MinZ) / 2;
            newSplitZ.RightNode.MinX = t.MinX;
            newSplitZ.RightNode.MinY = t.MinY;
            newSplitZ.RightNode.MinZ = newSplitZ.RightNode.SplitPosition;
            newSplitZ.RightNode.MaxX = t.MaxX;
            newSplitZ.RightNode.MaxY = t.MaxY;
            newSplitZ.RightNode.MaxZ = t.MaxZ;
            newSplitZ.RightNode.Type = 8;

            newSplitZ.LeftNode = FindTrianglesInsideNode(newSplitZ.LeftNode);
            newSplitZ.RightNode = FindTrianglesInsideNode(newSplitZ.RightNode);

            SplitList.Add(newSplitZ);
        }

        //int XDiff = Math.Abs(newSplitX.LeftNode.TriangleIndexList.Count - newSplitX.RightNode.TriangleIndexList.Count);
        //int YDiff = Math.Abs(newSplitY.LeftNode.TriangleIndexList.Count - newSplitY.RightNode.TriangleIndexList.Count);
        //int ZDiff = Math.Abs(newSplitZ.LeftNode.TriangleIndexList.Count - newSplitZ.RightNode.TriangleIndexList.Count);

        //int PairToUse = 0;
        //if (YDiff < XDiff)
        //{
        //    PairToUse = 1;
        //    if (ZDiff < YDiff)
        //        PairToUse = 2;
        //}
        //else if (ZDiff < XDiff)
        //{
        //    PairToUse = 2;
        //}

        //if (PairToUse == 0)        
        //    SplitList.Add(newSplitX);
        //else if (PairToUse == 1)
        //    SplitList.Add(newSplitY);
        //else if (PairToUse == 2)
        //    SplitList.Add(newSplitZ);

        int index = SplitList.Count - 1;

        if (SplitList[index].LeftNode.TriangleIndexList.Count > MaxTrianglesOnNode)
        {
            SplitList[index].LeftNode.isLeaf = false;
            SplitList[index].LeftNode.ChildIndex = (ushort)(SplitList.Count);
            SplitTreeNode(SplitList[index].LeftNode, MaxTrianglesOnNode, recursion + 1);
        }
        else
        {
            SplitList[index].LeftNode.TriangleAmount = (byte)SplitList[index].LeftNode.TriangleIndexList.Count();
            SplitList[index].LeftNode.isLeaf = true;
            SplitList[index].LeftNode.TriangleReferenceIndex = PositionOnList;
            TriangleIndexReferenceList.AddRange(SplitList[index].LeftNode.TriangleIndexList);
            PositionOnList = (ushort)TriangleIndexReferenceList.Count();
        }
        if (SplitList[index].RightNode.TriangleIndexList.Count > MaxTrianglesOnNode)
        {
            SplitList[index].RightNode.isLeaf = false;
            SplitList[index].RightNode.ChildIndex = (ushort)(SplitList.Count);
            SplitTreeNode(SplitList[index].RightNode, MaxTrianglesOnNode, recursion + 1);
        }
        else
        {
            SplitList[index].RightNode.TriangleAmount = (byte)SplitList[index].RightNode.TriangleIndexList.Count();
            SplitList[index].RightNode.TriangleReferenceIndex = PositionOnList;
            TriangleIndexReferenceList.AddRange(SplitList[index].RightNode.TriangleIndexList);
            PositionOnList = (ushort)TriangleIndexReferenceList.Count();
        }
    }

    private static TreeNode FindTrianglesInsideNode(TreeNode t)
    {
        List<ushort> NewIndexList = new List<ushort>();
        for (ushort i = 0; i < TriangleList.Count; i++)
        {
            if (IsTriangleInsideBox(TriangleList[i], t))
                NewIndexList.Add(i);
        }
        t.TriangleIndexList = NewIndexList;
        return t;
    }

    public static bool IsTriangleInsideBox(Triangle t, TreeNode bb)
    {
        if ((VertexList[(int)t.Vertex1].X >= bb.MinX & VertexList[(int)t.Vertex1].X <= bb.MaxX) &
            (VertexList[(int)t.Vertex1].Y >= bb.MinY & VertexList[(int)t.Vertex1].Y <= bb.MaxY) &
            (VertexList[(int)t.Vertex1].Z >= bb.MinZ & VertexList[(int)t.Vertex1].Z <= bb.MaxZ))
            return true;
        if ((VertexList[(int)t.Vertex2].X >= bb.MinX & VertexList[(int)t.Vertex2].X <= bb.MaxX) &
            (VertexList[(int)t.Vertex2].Y >= bb.MinY & VertexList[(int)t.Vertex2].Y <= bb.MaxY) &
            (VertexList[(int)t.Vertex2].Z >= bb.MinZ & VertexList[(int)t.Vertex2].Z <= bb.MaxZ))
            return true;
        if ((VertexList[(int)t.Vertex3].X >= bb.MinX & VertexList[(int)t.Vertex3].X <= bb.MaxX) &
            (VertexList[(int)t.Vertex3].Y >= bb.MinY & VertexList[(int)t.Vertex3].Y <= bb.MaxY) &
            (VertexList[(int)t.Vertex3].Z >= bb.MinZ & VertexList[(int)t.Vertex3].Z <= bb.MaxZ))
            return true;
        return false;
    }

    public static void CreateBSPFile(string OutputFile)
    {
        BinaryWriter BSPWriter = new BinaryWriter(new FileStream(Path.ChangeExtension(OutputFile, "BSP"), FileMode.Create));
        Console.WriteLine("Creating " + Path.ChangeExtension(OutputFile, "BSP"));

        const UInt32 RenderWare = 0x1C020037;

        long SavePosition = 0;

        float MaxX = VertexList[0].X;
        float MaxY = VertexList[0].Y;
        float MaxZ = VertexList[0].Z;
        float MinX = VertexList[0].X;
        float MinY = VertexList[0].Y;
        float MinZ = VertexList[0].Z;

        foreach (Vertex i in VertexList)
        {
            if (i.X > MaxX)
                MaxX = i.X;
            if (i.Y > MaxY)
                MaxY = i.Y;
            if (i.Z > MaxZ)
                MaxZ = i.Z;
            if (i.X < MinX)
                MinX = i.X;
            if (i.Y < MinY)
                MinY = i.Y;
            if (i.Z < MinZ)
                MinZ = i.Z;
        }

        //// WORLD SECTION
        BSPWriter.Write(0xb);
        long WorldSectionSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);

        //// MODEL HEADER STRUCT
        BSPWriter.Write(0x1);
        BSPWriter.Write(0x40);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(0x1);
        BSPWriter.Write(new byte[] { 0, 0, 0, 0x80 });
        BSPWriter.Write(new byte[] { 0, 0, 0, 0x80 });
        BSPWriter.Write(new byte[] { 0, 0, 0, 0x80 });
        BSPWriter.Write(TriangleList.Count);
        BSPWriter.Write(VertexList.Count);
        BSPWriter.Write(0);
        BSPWriter.Write(1);
        BSPWriter.Write(0);
        BSPWriter.Write(new byte[] { 0x41, 0, 0, 0x40 });
        BSPWriter.Write(MaxX);
        BSPWriter.Write(MaxY);
        BSPWriter.Write(MaxZ);
        //float32[3] // Boundary box maximum
        BSPWriter.Write(MinX);
        BSPWriter.Write(MinY);
        BSPWriter.Write(MinZ);
        //float32[3] // Boundary box minimum // Maximum values must be the bigger than minimum

        //No need for size here
        //// END MODEL HEADER STRUCT

        //// MATERIAL LIST SECTION
        BSPWriter.Write(0x8);
        long MaterialListSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);

        //// MATERIAL NUMBER STRUCT
        BSPWriter.Write(0x1);
        long MaterialNumberStructSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(AmountOfMeshes);
        for (int i = 0; i < AmountOfMeshes; i++)
            BSPWriter.Write(new byte[] { 0xff, 0xff, 0xff, 0xff });

        long MaterialNumberStructSize = BSPWriter.BaseStream.Position -MaterialNumberStructSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = MaterialNumberStructSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(MaterialNumberStructSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END MATERIAL NUMBER STRUCT

        for (int i = 0; i < AmountOfMeshes; i++)
        {
            //// MATERIAL REF SECTION // this section occours numMaterials times
            BSPWriter.Write(0x7);
            long MaterialRefSectionSizeLocation = BSPWriter.BaseStream.Position;
            BSPWriter.Write(0);
            BSPWriter.Write(RenderWare);

            //// MATERIAL STRUCT
            BSPWriter.Write(0x1);
            BSPWriter.Write(0x1c);
            BSPWriter.Write(RenderWare);
            BSPWriter.Write(0);
            BSPWriter.Write(new byte[] { 0xff, 0xff, 0xff, 0xff });
            BSPWriter.Write(new byte[] { 0x0C, 0xE7, 0xFA, 0x01 });
            BSPWriter.Write(0);
            BSPWriter.Write(Convert.ToSingle(1));
            BSPWriter.Write(Convert.ToSingle(1));
            BSPWriter.Write(Convert.ToSingle(1));

            //No need for size here
            //// END MATERIAL STRUCT
            
            //// MATERIAL EXTENSION // this section does absolutely nothing
            BSPWriter.Write(3);
            BSPWriter.Write(0);
            BSPWriter.Write(RenderWare);

            //No need for size here
            //// END MATERIAL EXTENSION

            long MaterialRefSectionSize = BSPWriter.BaseStream.Position -MaterialRefSectionSizeLocation - 8;
            SavePosition = BSPWriter.BaseStream.Position;
            BSPWriter.BaseStream.Position = MaterialRefSectionSizeLocation;
            BSPWriter.Write(Convert.ToUInt32(MaterialRefSectionSize));
            BSPWriter.BaseStream.Position = SavePosition;
            //// END MATERIAL REF SECTION
        }

        long MaterialListSize = BSPWriter.BaseStream.Position -MaterialListSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = MaterialListSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(MaterialListSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END MATERIAL LIST SECTION

        //// ATOMIC SECTION
        BSPWriter.Write(9);
        //Int32 0x09 // section identifier
        long AtomicSectionSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        //Int32 // section size
        BSPWriter.Write(RenderWare);
        //Int32 0x1400FFFF

        //// ATOMIC STRUCT
        BSPWriter.Write(1);
        long AtomicStructSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(0);
        BSPWriter.Write(TriangleList.Count);
        BSPWriter.Write(VertexList.Count);
        BSPWriter.Write(MinX);
        BSPWriter.Write(MinY);
        BSPWriter.Write(MinZ);
        BSPWriter.Write(MaxX);
        BSPWriter.Write(MaxY);
        BSPWriter.Write(MaxZ);
        BSPWriter.Write(new byte[] { 0x10, 0xF4, 0x12, 0x00 });
        BSPWriter.Write(0);

        foreach (Vertex v in VertexList)
        {
            BSPWriter.Write(v.X);
            BSPWriter.Write(v.Y);
            BSPWriter.Write(v.Z);
        }
        
        foreach (Triangle f in TriangleList)
        {
            BSPWriter.Write(Convert.ToUInt16(f.Vertex1));
            BSPWriter.Write(Convert.ToUInt16(f.Vertex2));
            BSPWriter.Write(Convert.ToUInt16(f.Vertex3));
            BSPWriter.Write(Convert.ToUInt16(f.MaterialIndex));
        }

        long AtomicStructSize = BSPWriter.BaseStream.Position -AtomicStructSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = AtomicStructSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(AtomicStructSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END ATOMIC STRUCT

        //// ATOMIC EXTENSION
        BSPWriter.Write(3);
        long AtomicExtensionSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);

        //// BIN MESH PLG SECTION
        BSPWriter.Write(0x50e);
        long BinMeshPLGSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(0); // flags
        BSPWriter.Write(AmountOfMeshes);
        //UInt32 // Number of objects/meshes (numMeshes; usually same number of materials)
        long TotalNumberOfTristripIndiciesLocation = BSPWriter.BaseStream.Position;
        long TotalNumberOfTristripIndicies = 0;
        BSPWriter.Write(0);
        //UInt32 // total number of indices

        for (int i = 0; i < AmountOfMeshes; i++)
        {
            List<Triangle> TriangleStream2 = new List<Triangle>();

            foreach (Triangle f in TriangleList)
                if (f.MaterialIndex == i)
                    TriangleStream2.Add(f);

            long NumberOfTristripIndiciesLocation = BSPWriter.BaseStream.Position;
            BSPWriter.Write(0);
            //    UInt32 // Number of vertex indices in this mesh (numIndices)
            BSPWriter.Write(i);
            //    UInt32 // material index

            //    UInt32[numIndices] // Vertex indices
            foreach (Triangle t in TriangleStream2)
            {
                BSPWriter.Write(t.Vertex1);
                BSPWriter.Write(t.Vertex2);
                BSPWriter.Write(t.Vertex3);
            }

            long NumberOfTristripIndicies = (BSPWriter.BaseStream.Position -NumberOfTristripIndiciesLocation - 8) / 4;
            SavePosition = BSPWriter.BaseStream.Position;
            BSPWriter.BaseStream.Position = NumberOfTristripIndiciesLocation;
            BSPWriter.Write(Convert.ToUInt32(NumberOfTristripIndicies));
            BSPWriter.BaseStream.Position = SavePosition;

            TotalNumberOfTristripIndicies += NumberOfTristripIndicies;
        }

        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = TotalNumberOfTristripIndiciesLocation;
        BSPWriter.Write(Convert.ToUInt32(TotalNumberOfTristripIndicies));
        BSPWriter.BaseStream.Position = SavePosition;

        long BinMeshPLGSize = BSPWriter.BaseStream.Position -BinMeshPLGSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = BinMeshPLGSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(BinMeshPLGSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END BIN MESH PLG SECTION

        // COLLISION PLG SECTION
        BSPWriter.Write(0x11D);
        long CollisionPLGSectionSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(new byte[] { 0x02, 0x70, 0x03, 0x00});

        // COLL TREE SECTION
        BSPWriter.Write(0x2C);
        long CollTreeSectionSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);

        // COLL STRUCT SECTION
        BSPWriter.Write(0x1);
        long CollStructSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(1); // flags; useMap
        BSPWriter.Write(MinX);
        BSPWriter.Write(MinY);
        BSPWriter.Write(MinZ);
        BSPWriter.Write(MaxX);
        BSPWriter.Write(MaxY);
        BSPWriter.Write(MaxZ);
        BSPWriter.Write(TriangleList.Count);
        BSPWriter.Write(SplitList.Count);
        
        for (int i = 0; i < SplitList.Count; i++)
        {
            if (SplitList[i].LeftNode.isLeaf)
            {
                BSPWriter.Write(SplitList[i].LeftNode.Type);
                BSPWriter.Write(SplitList[i].LeftNode.TriangleAmount);
                BSPWriter.Write(SplitList[i].LeftNode.TriangleReferenceIndex);
                BSPWriter.Write(SplitList[i].LeftNode.SplitPosition);
            }
            else
            {
                BSPWriter.Write(SplitList[i].LeftNode.Type);
                BSPWriter.Write((byte)0xFF);
                BSPWriter.Write(SplitList[i].LeftNode.ChildIndex);
                BSPWriter.Write(SplitList[i].LeftNode.SplitPosition);
            }

            if (SplitList[i].RightNode.isLeaf)
            {
                BSPWriter.Write(SplitList[i].RightNode.Type);
                BSPWriter.Write(SplitList[i].RightNode.TriangleAmount);
                BSPWriter.Write(SplitList[i].RightNode.TriangleReferenceIndex);
                BSPWriter.Write(SplitList[i].RightNode.SplitPosition);
            }
            else
            {
                BSPWriter.Write(SplitList[i].RightNode.Type);
                BSPWriter.Write((byte)0xFF);
                BSPWriter.Write(SplitList[i].RightNode.ChildIndex);
                BSPWriter.Write(SplitList[i].RightNode.SplitPosition);
            }
        }
        foreach (ushort i in TriangleIndexReferenceList)
            BSPWriter.Write(i);
        
        long CollStructSize = BSPWriter.BaseStream.Position - CollStructSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = CollStructSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(CollStructSize));
        BSPWriter.BaseStream.Position = SavePosition;
        // END COLL STRUCT SECTION

        long CollTreeSize = BSPWriter.BaseStream.Position - CollTreeSectionSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = CollTreeSectionSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(CollTreeSize));
        BSPWriter.BaseStream.Position = SavePosition;
        // END COLL TREE SECTION

        long CollisionPLGSize = BSPWriter.BaseStream.Position - CollisionPLGSectionSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = CollisionPLGSectionSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(CollisionPLGSize));
        BSPWriter.BaseStream.Position = SavePosition;
        // END COLLISION PLG SECTION

        // USER DATA PLG SECTION
        BSPWriter.Write(0x11F);
        long UserDataPLHSizeLocation = BSPWriter.BaseStream.Position;
        BSPWriter.Write(0);
        BSPWriter.Write(RenderWare);
        BSPWriter.Write(2);
        BSPWriter.Write(0x0A);
        foreach (char c in "attribute")
            BSPWriter.Write((byte)c);
        BSPWriter.Write((byte)0);
        BSPWriter.Write(1);
        BSPWriter.Write(TriangleList.Count());
        foreach (Triangle t in TriangleList)
            BSPWriter.Write(new byte[] { 01, 00, 02, 00 });
        BSPWriter.Write(0x0D);
        foreach (char c in "FVF.UserData")
            BSPWriter.Write((byte)c);
        BSPWriter.Write((byte)0);
        BSPWriter.Write(1);
        BSPWriter.Write(1);
        BSPWriter.Write(new byte[] { 3, 0x30, 0, 0 });

        long UserDataPLGSize = BSPWriter.BaseStream.Position - UserDataPLHSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = UserDataPLHSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(UserDataPLGSize));
        BSPWriter.BaseStream.Position = SavePosition;
        // END USER DATA PLG SECTION

        long AtomicExtensionSize = BSPWriter.BaseStream.Position -AtomicExtensionSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = AtomicExtensionSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(AtomicExtensionSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END ATOMIC EXTENSION

        long AtomicSectionSize = BSPWriter.BaseStream.Position -AtomicSectionSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = AtomicSectionSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(AtomicSectionSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END ATOMIC SECTION

        //// WORLD EXTENSION // this section does absolutely nothing
        BSPWriter.Write(3);
        //Int32 0x03 // section identifier
        BSPWriter.Write(0);
        //Int32 // section size (0x00)
        BSPWriter.Write(RenderWare);
        //Int32 0x1400FFFF

        //// END WORLD EXTENSION

        long WorldSectionSize = BSPWriter.BaseStream.Position -WorldSectionSizeLocation - 8;
        SavePosition = BSPWriter.BaseStream.Position;
        BSPWriter.BaseStream.Position = WorldSectionSizeLocation;
        BSPWriter.Write(Convert.ToUInt32(WorldSectionSize));
        BSPWriter.BaseStream.Position = SavePosition;
        //// END WORLD SECTION
    }

    public static void ConvertBSPtoOBJ(string FileName)
    {
        AmountOfMeshes = 0;
        VertexList.Clear();
        TriangleList.Clear();

        Console.WriteLine("Reading " + FileName);
        //try
        //{
        if (ReadBSPFile(FileName))
        {
            CreateOBJFile(FileName);
            Console.WriteLine("Success.");
        }
        else
            Console.WriteLine("Error.");
        //}
        //catch
        //{
        //    Console.WriteLine("Error.");
        //}
    }

    public static bool ReadBSPFile(string InputFileName)
    {
        BinaryReader BSPReader = new BinaryReader(new FileStream(InputFileName, FileMode.Open));

        BSPReader.BaseStream.Position = 0x4;
        if (BSPReader.ReadUInt32() + 0xC != BSPReader.BaseStream.Length)
            return false;

        BSPReader.BaseStream.Position = 0x28;
        UInt32 NumTriangles = BSPReader.ReadUInt32();
        UInt32 NumVertices = BSPReader.ReadUInt32();
        BSPReader.BaseStream.Position = 0x70;
        AmountOfMeshes = BSPReader.ReadInt32();

        BSPReader.BaseStream.Position += 4 * AmountOfMeshes;
        BSPReader.BaseStream.Position += AmountOfMeshes * 0x40;

        BSPReader.BaseStream.Position += 0x1C;

        if (BSPReader.ReadUInt32() != NumTriangles)
            return false;

        if (BSPReader.ReadUInt32() != NumVertices)
            return false;

        BSPReader.BaseStream.Position += 0x20;

        for (int i = 0; i < NumVertices; i++)
            VertexList.Add(new Vertex(BSPReader.ReadSingle(), BSPReader.ReadSingle(), BSPReader.ReadSingle()));
        
        for (int i = 0; i < NumTriangles; i++)
            TriangleList.Add(new Triangle(BSPReader.ReadUInt16(), BSPReader.ReadUInt16(), BSPReader.ReadUInt16(), BSPReader.ReadUInt16()));
        return true;
    }
        
    public static void CreateOBJFile(string OutputFileName)
    {
        StreamWriter OBJWriter = new StreamWriter((Path.ChangeExtension(OutputFileName, "OBJ")), false);

        string FileName = Path.GetFileNameWithoutExtension(OutputFileName);

        OBJWriter.WriteLine("#Exported by ShadowCollisionTool");
        OBJWriter.WriteLine("#Number of vertices: " + VertexList.Count.ToString());
        OBJWriter.WriteLine("#Number of faces: " + TriangleList.Count.ToString());
        OBJWriter.WriteLine();
        
        foreach (Vertex i in VertexList)
            OBJWriter.WriteLine("v " + i.X.ToString() + " " + i.Y.ToString() + " " + i.Z.ToString());

        OBJWriter.WriteLine();        

        for (int i = 0; i < AmountOfMeshes; i++)
        {
            OBJWriter.WriteLine(String.Format("g {0}_{1, 2:D2}", FileName, i));
            foreach (Triangle j in TriangleList)
                if (j.MaterialIndex == i)
                    OBJWriter.WriteLine(String.Format("f {0} {1} {2}", j.Vertex1 + 1, j.Vertex2 + 1, j.Vertex3 + 1));
            OBJWriter.WriteLine();
        }

        OBJWriter.Close();
    }
}