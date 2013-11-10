﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ORTS
{
    #region SkyConstants
    static class SkyConstants
    {
        // Sky dome constants
        public const int skyRadius = 6000;
        public const int skySides = 24;
    }
    #endregion

    #region SkyDrawer
    public class SkyDrawer
    {
        Viewer3D Viewer;
        Material skyMaterial;

        // Classes reqiring instantiation
        public SkyMesh SkyMesh;
        WorldLatLon worldLoc; // Access to latitude and longitude calcs (MSTS routes only)
        SunMoonPos skyVectors;

		int seasonType; //still need to remember it as MP now can change it.
        #region Class variables
        // Latitude of current route in radians. -pi/2 = south pole, 0 = equator, pi/2 = north pole.
        // Longitude of current route in radians. -pi = west of prime, 0 = prime, pi = east of prime.
        public double latitude, longitude;
        // Date of activity
        public struct Date
        {
            // Day, month, year. Format: DD MM YYYY, no leading zeros. 
            public int year;
            public int month;
            public int day;
            // Ordinal date. Range: 0 to 366.
            public int ordinalDate;
        };
        public Date date;
        // Size of the sun- and moon-position lookup table arrays.
        // Must be an integral divisor of 1440 (which is the number of minutes in a day).
        private int maxSteps = 72;
        private double oldClockTime;
        int step1, step2;
        // Phase of the moon
        public int moonPhase;
        // Wind speed and direction
        public float windSpeed;
        public float windDirection;
        // Overcast factor
        public float overcast;
        public float fogCoeff;

        // These arrays and vectors define the position of the sun and moon in the world
        Vector3[] solarPosArray = new Vector3[72];
        Vector3[] lunarPosArray = new Vector3[72];
        public Vector3 solarDirection;
        public Vector3 lunarDirection;
        #endregion

        #region Constructor
        /// <summary>
        /// SkyDrawer constructor
        /// </summary>
        public SkyDrawer(Viewer3D viewer)
        {
            Viewer = viewer;
            skyMaterial = viewer.MaterialManager.Load("Sky");

            // Instantiate classes
            SkyMesh = new SkyMesh( Viewer.RenderProcess);
            skyVectors = new SunMoonPos();

            // Set default values
            seasonType = (int)Viewer.Simulator.Season;
            date.ordinalDate = 82 + seasonType * 91;
            // TODO: Set the following three externally from ORTS route files (future)
            date.month = 1 + date.ordinalDate / 30;
            date.day = 21;
            date.year = 2010;
            // Default wind speed and direction
            windSpeed = 5.0f; // m/s (approx 11 mph)
            windDirection = 4.7f; // radians (approx 270 deg, i.e. westerly)
       }
        #endregion

        /// <summary>
        /// Used to update information affecting the SkyMesh
        /// </summary>
        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
			if (seasonType != (int)Viewer.Simulator.Season)
			{
				seasonType = (int)Viewer.Simulator.Season;
				date.ordinalDate = 82 + seasonType * 91;
				// TODO: Set the following three externally from ORTS route files (future)
				date.month = 1 + date.ordinalDate / 30;
				date.day = 21;
				date.year = 2010;
			}
            // Adjust dome position so the bottom edge is not visible
			Vector3 ViewerXNAPosition = new Vector3(Viewer.Camera.Location.X, Viewer.Camera.Location.Y - 100, -Viewer.Camera.Location.Z);
            Matrix XNASkyWorldLocation = Matrix.CreateTranslation(ViewerXNAPosition);

            if (worldLoc == null)
            {
                // First time around, initialize the following items:
                worldLoc = new WorldLatLon();
                oldClockTime = Viewer.Simulator.ClockTime % 86400;
                while (oldClockTime < 0) oldClockTime += 86400;
                step1 = step2 = (int)(oldClockTime / 1200);
                step2++;
                // Get the current latitude and longitude coordinates
                worldLoc.ConvertWTC(Viewer.Camera.TileX, Viewer.Camera.TileZ, Viewer.Camera.Location, ref latitude, ref longitude);
                // Fill in the sun- and moon-position lookup tables
                for (int i = 0; i < maxSteps; i++)
                {
                    solarPosArray[i] = SunMoonPos.SolarAngle(latitude, longitude, ((float)i / maxSteps), date);
                    lunarPosArray[i] = SunMoonPos.LunarAngle(latitude, longitude, ((float)i / maxSteps), date);
                }
                // Phase of the moon is generated at random
                Random random = new Random();
                moonPhase = random.Next(8);
                if (moonPhase == 6 && date.ordinalDate > 45 && date.ordinalDate < 330)
                    moonPhase = 3; // Moon dog only occurs in winter
                // Overcast factor: 0.0=almost no clouds; 0.1=wispy clouds; 1.0=total overcast
                overcast = Viewer.World.WeatherControl.overcast;
                fogCoeff = Viewer.World.WeatherControl.fogCoeff;
            }

			if (MultiPlayer.MPManager.IsClient() && MultiPlayer.MPManager.Instance().weatherChanged)
			{
				//received message about weather change
				if ( MultiPlayer.MPManager.Instance().overCast >= 0)
				{
					overcast = MultiPlayer.MPManager.Instance().overCast;
				}
                //received message about weather change
                if (MultiPlayer.MPManager.Instance().newFog > 0)
                {
                    fogCoeff = MultiPlayer.MPManager.Instance().newFog;
                }
                try
                {
                    if (MultiPlayer.MPManager.Instance().overCast >= 0 || MultiPlayer.MPManager.Instance().newFog > 0) 
                        MultiPlayer.MPManager.Instance().weatherChanged = false;
                }
                catch { }

            }

////////////////////// T E M P O R A R Y ///////////////////////////

            // The following keyboard commands are used for viewing sky and weather effects in "demo" mode
            // The ( + ) key speeds the time forward, the ( - ) key reverses the time.
            // When the Ctrl key is also pressed, the + and - keys control the amount of overcast.

			if (UserInput.IsDown(UserCommands.DebugOvercastIncrease) && !MultiPlayer.MPManager.IsClient())
			{
                fogCoeff = MathHelper.Clamp(fogCoeff / 1.02f, 0.002f, 1);
                overcast = MathHelper.Clamp(overcast + 0.005f, 0, 1);
			}
            if (UserInput.IsDown(UserCommands.DebugFogIncrease) && !MultiPlayer.MPManager.IsClient())
            {
                fogCoeff = MathHelper.Clamp(fogCoeff / 1.05f, 0.002f, 1);
                if (fogCoeff < 0.002f) fogCoeff = 0.002f;
            }
            if (UserInput.IsDown(UserCommands.DebugFogDecrease) && !MultiPlayer.MPManager.IsClient())
            {
                fogCoeff = MathHelper.Clamp(fogCoeff * 1.05f, 0.002f, 1);
            }
            if (UserInput.IsDown(UserCommands.DebugOvercastDecrease) && !MultiPlayer.MPManager.IsClient())
			{
                fogCoeff = MathHelper.Clamp(fogCoeff * 1.02f, 0.002f, 1);
                overcast = MathHelper.Clamp(overcast - 0.005f, 0, 1);
			}
			if (UserInput.IsDown(UserCommands.DebugClockForwards) && !MultiPlayer.MPManager.IsMultiPlayer()) //dosen't make sense in MP mode
			{
				Viewer.Simulator.ClockTime += 120; // Two-minute (120 second) increments
                if (Viewer.World.Precipitation != null) Viewer.World.Precipitation.Reset();
			}
			if (UserInput.IsDown(UserCommands.DebugClockBackwards) && !MultiPlayer.MPManager.IsMultiPlayer())
			{
				Viewer.Simulator.ClockTime -= 120;
                if (Viewer.World.Precipitation != null) Viewer.World.Precipitation.Reset();
			}
            if (MultiPlayer.MPManager.IsServer())
            {
                if (UserInput.IsReleased(UserCommands.DebugFogDecrease) || UserInput.IsReleased(UserCommands.DebugFogIncrease)
                    || UserInput.IsReleased(UserCommands.DebugOvercastDecrease) || UserInput.IsReleased(UserCommands.DebugOvercastIncrease))
                {
                    MultiPlayer.MPManager.Instance().SetEnvInfo(overcast, fogCoeff);
                    MultiPlayer.MPManager.Notify((new MultiPlayer.MSGWeather(-1, overcast, fogCoeff)).ToString());//server notify others the weather has changed
                }
            }

////////////////////////////////////////////////////////////////////

            // Current solar and lunar position are calculated by interpolation in the lookup arrays.
            // Using the Lerp() function, so need to calculate the in-between differential
            float diff = (float)(Viewer.Simulator.ClockTime - oldClockTime) / 1200;
            // The rest of this increments/decrements the array indices and checks for overshoot/undershoot.
            if (Viewer.Simulator.ClockTime >= (oldClockTime + 1200)) // Plus key, or normal forward in time
            {
                step1++;
                step2++;
                oldClockTime = Viewer.Simulator.ClockTime;
                diff = 0;
                if (step1 == maxSteps - 1) // Midnight. Value is 71 for maxSteps = 72
                {
                    step2 = 0;
                }
                if (step1 == maxSteps) // Midnight.
                {
                    step1 = 0;
                }
            }
            if (Viewer.Simulator.ClockTime <= (oldClockTime - 1200)) // Minus key
            {
                step1--;
                step2--;
                oldClockTime = Viewer.Simulator.ClockTime;
                diff = 0;
                if (step1 < 0) // Midnight.
                {
                    step1 = 71;
                }
                if (step2 < 0) // Midnight.
                {
                    step2 = 71;
                }
            }
            solarDirection.X = MathHelper.Lerp(solarPosArray[step1].X, solarPosArray[step2].X, diff);
            solarDirection.Y = MathHelper.Lerp(solarPosArray[step1].Y, solarPosArray[step2].Y, diff);
            solarDirection.Z = MathHelper.Lerp(solarPosArray[step1].Z, solarPosArray[step2].Z, diff);
            lunarDirection.X = MathHelper.Lerp(lunarPosArray[step1].X, lunarPosArray[step2].X, diff);
            lunarDirection.Y = MathHelper.Lerp(lunarPosArray[step1].Y, lunarPosArray[step2].Y, diff);
            lunarDirection.Z = MathHelper.Lerp(lunarPosArray[step1].Z, lunarPosArray[step2].Z, diff);

            frame.AddPrimitive(skyMaterial, SkyMesh, RenderPrimitiveGroup.Sky, ref XNASkyWorldLocation);
        }

        [CallOnThread("Loader")]
        internal void Mark()
        {
            skyMaterial.Mark();
        }
    }
    #endregion

    #region SkyMesh
    public class SkyMesh: RenderPrimitive 
    {
        private VertexBuffer SkyVertexBuffer;
        private static VertexDeclaration SkyVertexDeclaration;
        private static IndexBuffer SkyIndexBuffer;
        private static int SkyVertexStride;  // in bytes
        public int drawIndex;

        VertexPositionNormalTexture[] vertexList;
        private static short[] triangleListIndices; // Trilist buffer.

        // Sky dome geometry is based on two global variables: the radius and the number of sides
        public int skyRadius = SkyConstants.skyRadius;
        private static int skySides = SkyConstants.skySides;
        public int cloudDomeRadiusDiff = 600; // Amount by which cloud dome radius is smaller than sky dome
        // skyLevels: Used for iterating vertically through the "levels" of the hemisphere polygon
        private static int skyLevels = ((SkyConstants.skySides / 4) - 1);
        // Number of vertices in the sky hemisphere. (each dome = 145 for 24-sided sky dome: 24 x 6 + 1)
        // plus four more for the moon quad
        private static int numVertices = 4 + 2 * (int)((Math.Pow(skySides, 2) / 4) + 1);
        // Number of point indices (each dome = 792 for 24 sides: 5 levels of 24 triangle pairs each
        // plus 24 triangles at the zenith)
        // plus six more for the moon quad
        private static short indexCount = 6 + 2 * ((SkyConstants.skySides * 2 * 3 * ((SkyConstants.skySides / 4) - 1)) + 3 * SkyConstants.skySides);

        /// <summary>
        /// Constructor.
        /// </summary>
        public SkyMesh(RenderProcess renderProcess)
        {
            // Initialize the vertex and point-index buffers
            vertexList = new VertexPositionNormalTexture[numVertices];
            triangleListIndices = new short[indexCount];
            // Sky dome
            DomeVertexList(0, skyRadius, 1.0f);
            DomeTriangleList(0, 0);
            // Cloud dome
            DomeVertexList((numVertices - 4) / 2, skyRadius - cloudDomeRadiusDiff, 0.4f);
            DomeTriangleList((short)((indexCount - 6) / 2), 1);
            // Moon quad
            MoonLists(numVertices - 5, indexCount - 6);//(144, 792);
            // Meshes have now been assembled, so put everything into vertex and index buffers
            InitializeVertexBuffers(renderProcess.GraphicsDevice);
        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.VertexDeclaration = SkyVertexDeclaration;
            graphicsDevice.Vertices[0].SetSource(SkyVertexBuffer, 0, SkyVertexStride);
            graphicsDevice.Indices = SkyIndexBuffer;

            switch (drawIndex)
            {
                case 1: // Sky dome
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        (numVertices - 4) / 2,
                        0,
                        (indexCount - 6) / 6);
                    break;
                case 2: // Moon
                    graphicsDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0,
                    numVertices - 4,
                    4,
                    indexCount - 6,
                    2);
                    break;
                case 3: // Clouds Dome
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        (numVertices - 4) / 2,
                        (numVertices - 4) / 2,
                        (indexCount - 6) / 2,
                        (indexCount - 6) / 6);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates the vertex list for each sky dome.
        /// </summary>
        /// <param name="index">The starting vertex number</param>
        /// <param name="radius">The radius of the dome</param>
        /// <param name="oblate">The amount the dome is flattened</param>
        private void DomeVertexList(int index, int radius, float oblate)
        {
            int vertexIndex = index;
            // for each vertex
            for (int i = 0; i < (skySides / 4); i++) // (=6 for 24 sides)
                for (int j = 0; j < skySides; j++) // (=24 for top overlay)
                {
                    // The "oblate" factor is used to flatten the dome to an ellipsoid. Used for the inner (cloud)
                    // dome only. Gives the clouds a flatter appearance.
                    float y = (float)Math.Sin(MathHelper.ToRadians((360 / skySides) * i)) * radius * oblate;
                    float yRadius = radius * (float)Math.Cos(MathHelper.ToRadians((360 / skySides) * i));
                    float x = (float)Math.Cos(MathHelper.ToRadians((360 / skySides) * (skySides - j))) * yRadius;
                    float z = (float)Math.Sin(MathHelper.ToRadians((360 / skySides) * (skySides - j))) * yRadius;

                    // UV coordinates - top overlay
                    float uvRadius;
                    uvRadius = 0.5f - (float)(0.5f * i) / (skySides / 4);
                    float uv_u = 0.5f - ((float)Math.Cos(MathHelper.ToRadians((360 / skySides) * (skySides - j))) * uvRadius);
                    float uv_v = 0.5f - ((float)Math.Sin(MathHelper.ToRadians((360 / skySides) * (skySides - j))) * uvRadius);

                    // Store the position, texture coordinates and normal (normalized position vector) for the current vertex
                    vertexList[vertexIndex].Position = new Vector3(x, y, z);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(uv_u, uv_v);
                    vertexList[vertexIndex].Normal = Vector3.Normalize(new Vector3(x, y, z));
                    vertexIndex++;
                }
            // Single vertex at zenith
            vertexList[vertexIndex].Position = new Vector3(0, radius, 0);
            vertexList[vertexIndex].Normal = new Vector3(0, 1, 0);
            vertexList[vertexIndex].TextureCoordinate = new Vector2(0.5f, 0.5f); // (top overlay)
        }

        /// <summary>
        /// Creates the triangle index list for each dome.
        /// </summary>
        /// <param name="index">The starting triangle index number</param>
        /// <param name="pass">A multiplier used to arrive at the starting vertex number</param>
        static void DomeTriangleList(short index, short pass)
        {
            // ----------------------------------------------------------------------
            // 24-sided sky dome mesh is built like this:        48 49 50
            // Triangles are wound couterclockwise          71 o--o--o--o
            // because we're looking at the inner              | /|\ | /|
            // side of the hemisphere. Each time               |/ | \|/ |
            // we circle around to the start point          47 o--o--o--o 26
            // on the mesh we have to reset the                |\ | /|\ |
            // vertex number back to the beginning.            | \|/ | \|
            // Using WAC's sw,se,nw,ne coordinate    nw ne  23 o--o--o--o 
            // convention.-->                        sw se        0  1  2
            // ----------------------------------------------------------------------
            short iIndex = index;
            short baseVert = (short)(pass * (short)((numVertices - 4) / 2));
            for (int i = 0; i < skyLevels; i++) // (=5 for 24 sides)
                for (int j = 0; j < skySides; j++) // (=24 for 24 sides)
                {
                    // Vertex indices, beginning in the southwest corner
                    short sw = (short)(baseVert + (j + i * (skySides)));
                    short nw = (short)(sw + skySides); // top overlay mapping
                    short ne = (short)(nw + 1);

                    short se = (short)(sw + 1);

                    if (((i & 1) == (j & 1)))  // triangles alternate
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % skySides == 0) ? (short)(ne - skySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % skySides == 0) ? (short)(se - skySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % skySides == 0) ? (short)(ne - skySides) : ne;
                    }
                    else
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % skySides == 0) ? (short)(se - skySides) : se;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % skySides == 0) ? (short)(se - skySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % skySides == 0) ? (short)(ne - skySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                    }
                }
            //Zenith triangles (=24 for 24 sides)
            for (int i = 0; i < skySides; i++)
            {
                short sw = (short)(baseVert + (((skySides) * skyLevels) + i));
                short se = (short)(sw + 1);

                triangleListIndices[iIndex++] = sw;
                triangleListIndices[iIndex++] = ((se - baseVert) % skySides == 0) ? (short)(se - skySides) : se;
                triangleListIndices[iIndex++] = (short)(baseVert + (short)((numVertices - 5) / 2)); // The zenith
            }
        }

        /// <summary>
        /// Creates the moon vertex and triangle index lists.
        /// <param name="vertexIndex">The starting vertex number</param>
        /// <param name="iIndex">The starting triangle index number</param>
        /// </summary>
        private void MoonLists(int vertexIndex, int iIndex)
        {
            // Moon vertices
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    vertexIndex++;
                    vertexList[vertexIndex].Position = new Vector3(i, j, 0);
                    vertexList[vertexIndex].Normal = new Vector3(0, 0, 1);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(i, j);
                }

            // Moon indices - clockwise winding
            short msw = (short)(numVertices - 4);
            short mnw = (short)(msw + 1);
            short mse = (short)(mnw + 1);
            short mne = (short)(mse + 1);
            triangleListIndices[iIndex++] = msw;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mne;
        }

        /// <summary>
        /// Initializes the sky dome, cloud dome and moon vertex and triangle index list buffers.
        /// </summary>
        private void InitializeVertexBuffers(GraphicsDevice graphicsDevice)
        {
            if (SkyVertexDeclaration == null)
            {
                SkyVertexDeclaration = new VertexDeclaration(graphicsDevice, VertexPositionNormalTexture.VertexElements);
                SkyVertexStride = VertexPositionNormalTexture.SizeInBytes;
            }
            // Initialize the vertex and index buffers, allocating memory for each vertex and index
            SkyVertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.SizeInBytes * vertexList.Length, BufferUsage.WriteOnly);
            SkyVertexBuffer.SetData(vertexList);
            if (SkyIndexBuffer == null)
            {
                SkyIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexCount, BufferUsage.WriteOnly);
                SkyIndexBuffer.SetData(triangleListIndices);
            }
        }

    } // SkyMesh
    #endregion
}
