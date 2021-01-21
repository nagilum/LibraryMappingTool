namespace lmt
{
    public class Config
    {
        /// <summary>
        /// Database info and credentials.
        /// </summary>
        public DatabaseConfig Database { get; set; }

        /// <summary>
        /// Folders to scan for .DLL files.
        /// </summary>
        public FolderEntry[] Folders { get; set; }

        #region Helper classes

        public class FolderEntry
        {
            /// <summary>
            /// Full path of the folder to scan.
            /// </summary>
            public string Path { get; set; }
        }

        public class DatabaseConfig
        {
            /// <summary>
            /// Host, or IP, to connect to.
            /// </summary>
            public string Hostname { get; set; }

            /// <summary>
            /// Database to connect to.
            /// </summary>
            public string Database { get; set; }

            /// <summary>
            /// User to login with.
            /// </summary>
            public string Username { get; set; }

            /// <summary>
            /// Password to use.
            /// </summary>
            public string Password { get; set; }
        }

        #endregion
    }
}