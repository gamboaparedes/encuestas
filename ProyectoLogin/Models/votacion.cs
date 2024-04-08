namespace ProyectoLogin.Models
{
    public class votacion
    {
        public int Id { get; set; }

        public int IdVoto { get; set; }

        public string informacion { get; set; }

        public string navegacion { get; set; } = null!;

        public int encuesta { get; set; } 

        public string palabraClave { get; set; } = null!;
        public string usuario { get; set; } = null!;

        public int Count { get; set; }
        public string CandidatoNombre { get; set; }
        public string CandidatoImagen { get; set; }
        public string PartidoImagen { get; set; } 
    }
}
