namespace CarritoCaotico
{
    /// <summary>Supermercados disponibles. Cada uno es una escena distinta.</summary>
    public enum StoreId
    {
        Clasico = 0,
        Halloween = 1
    }

    /// <summary>Datos de cada supermercado: escena, nombre, imágenes de carga y consejos.</summary>
    public static class StoreCatalog
    {
        public static readonly StoreId[] All = { StoreId.Clasico, StoreId.Halloween };

        public static string SceneName(StoreId store) =>
            store == StoreId.Halloween ? "CarritoCaotico_Halloween" : "CarritoCaotico_Prueba";

        /// <summary>Ruta de la escena (válida en el Editor y en compilaciones si está en Build Settings).</summary>
        public static string ScenePath(StoreId store) => $"Assets/_CarritoCaotico/Scenes/{SceneName(store)}.unity";

        public static string DisplayName(StoreId store) =>
            store == StoreId.Halloween ? "Súper Embrujado" : "Súper Caótico";

        public static string Description(StoreId store) =>
            store == StoreId.Halloween
                ? "Halloween: calabazas, pociones, fantasmas y charcos de baba."
                : "El súper de siempre: seis pasillos, charcos y reponedores.";

        /// <summary>Prefijo de las imágenes de carga en Resources/Carga.</summary>
        public static string LoadingPrefix(StoreId store) =>
            store == StoreId.Halloween ? "halloween" : "clasico";

        public static string[] Tips(StoreId store)
        {
            if (store == StoreId.Halloween)
            {
                return new[]
                {
                    "Los charcos de baba verde hacen que el carrito patine.",
                    "Los fantasmas cruzan los pasillos: ¡esquívenlos!",
                    "Las calabazas pesan mucho: el carrito frenará peor.",
                    "Las lápidas no se mueven. Los carritos abandonados, sí.",
                    "El Agarrador es el único que ve la lista: ¡hablen!"
                };
            }
            return new[]
            {
                "Solo el Agarrador ve la lista: el Empujador necesita indicaciones.",
                "Los charcos azules hacen que el carrito patine.",
                "Los rivales se llevan productos de tu lista: ¡sé más rápido!",
                "Suelta los productos cerca del carrito para que caigan en la canasta.",
                "Pausa la partida con Esc o P si necesitan respirar.",
                "Desde el nivel 4 los controles pueden cambiar sin avisar."
            };
        }
    }
}
