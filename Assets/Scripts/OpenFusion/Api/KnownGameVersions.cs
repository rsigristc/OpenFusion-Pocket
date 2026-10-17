/*
MIT License

Copyright (c) 2020-2025 OpenFusion Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
using System.Collections.Generic;

namespace OpenFusion.Api
{
    // Names from OpenFusionLauncher resources/defaults/versions. These built-ins
    // are advertised by the public APIs but do not have /versions endpoints.
    // Source: https://github.com/OpenFusionProject/OpenFusionLauncher/tree/master/resources/defaults/versions
    public static class KnownGameVersions
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "ec8063b2-54d4-4ee1-8d9e-381f5babd420", "beta-20100104" },
            { "793ed605-9a36-490c-95e1-6d1df8d7568b", "beta-20100119" },
            { "3d18aab5-95a5-49cb-8cde-ddcf809ad623", "beta-20100207" },
            { "b3d94727-906d-4d94-ab8b-2360bf15cca1", "beta-20100307" },
            { "908a5fec-5723-4c4a-af51-b268b8904776", "beta-20100322" },
            { "7d79b3d1-1e20-4d25-ba03-f2d4a6cf8cf5", "beta-20100413" },
            { "23bbdbe5-8639-4f13-8dee-1e9ff72102ac", "beta-20100502" },
            { "c5981846-a3e0-4598-a6a4-1b0eaf9dba2c", "beta-20100524" },
            { "83797b94-f137-4637-b376-38c9b6ed70b2", "beta-20100604" },
            { "967240a1-8047-4de3-a070-06276faf58c3", "beta-20100616" },
            { "72a5c13a-bdcf-4b23-acc7-c95d9e9c72bb", "beta-20100711" },
            { "ec1bd459-9f07-4c2e-9073-99cd7d2062be", "beta-20100728" },
            { "86c57a2c-ce46-4429-b5b5-3936fda8e7e9", "beta-20100909" },
            { "2a2be828-bd05-42a6-9b33-b837f2766773", "beta-20101003" },
            { "61b8e04a-4c75-4f41-b0a8-65eb9ba9ca60", "beta-20101011" },
            { "bf5e00c8-29e8-4541-97f6-15ea26509cdd", "beta-20101028" },
            { "f0d3541f-152d-42ef-816f-63272c392a2c", "beta-20101123" },
            { "ff73017d-dea2-4a72-a23d-066944460c67", "beta-20110213" },
            { "1bb7cda3-c463-4856-b8bc-6918fc06f057", "beta-20110314" },
            { "7ce68356-872f-427d-9935-581f24d93bff", "beta-20110330" },
            { "232f6405-9eec-4cbc-a4d7-e1003446474a", "beta-20110424" },
            { "b9d9c8ee-0a73-4d4c-b5e3-fa1d25213870", "beta-20110523" },
            { "f7e6c55c-5dfc-404f-b0d5-14f3a1fa791c", "beta-20110725" },
            { "d1ccb15f-bbfe-4cf7-9c25-84d0a0f759da", "beta-20110818" },
            { "bc988161-b425-4b50-88c6-4c9947cb73ea", "beta-20110912" },
            { "6543a2bb-d154-4087-b9ee-3c8aa778580a", "beta-20111013" },
        };
        public static string NameFor(string uuid) { return uuid != null && Names.TryGetValue(uuid, out var name) ? name : null; }
    }
}
