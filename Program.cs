using Microsoft.OpenApi.Extensions;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//Variables
string carpetaDestino = Path.Combine(Directory.GetCurrentDirectory(), "Archivos");
string carpetaData = Path.Combine(Directory.GetCurrentDirectory(), "data");
string archivoStatus = Path.Combine(carpetaData, "status_generate.json");
string archivoLog = Path.Combine(carpetaDestino, "log.txt");
string statusFileMath = Path.Combine(carpetaData, "status_process.json");


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//Generar archivos
app.MapPost("/generar-archivos", async (HttpContext httpContext) =>
{
    int totalArchivos = 3000;

    // Crear las carpetas si no existen
    if (!Directory.Exists(carpetaDestino))
        Directory.CreateDirectory(carpetaDestino);

    if (!Directory.Exists(carpetaData))
        Directory.CreateDirectory(carpetaData);

    // Limpiar los archivos existentes
    if (File.Exists(archivoLog))
        File.Delete(archivoLog);

    // Crear un objeto de bloqueo
    object lockObject = new object();
    int currentFile = 0;

    // Inicializar el archivo de estado
    var status = new { currentFile = 0, totalFiles = totalArchivos, completed = false };
    await File.WriteAllTextAsync(archivoStatus, JsonSerializer.Serialize(status));

    // Iniciar el cronómetro
    Stopwatch stopwatch = Stopwatch.StartNew();

    // Generar archivos de forma paralela
    await Task.Run(() =>
    {
        Parallel.For(0, totalArchivos, i =>
        {
            string nombreArchivo = Path.Combine(carpetaDestino, $"{i + 1}.txt");

            // Crear el archivo
            File.WriteAllText(nombreArchivo, $"Contenido del archivo {i + 1}");

            // Registrar en el archivo de log y actualizar el progreso
            lock (lockObject)
            {
                string logEntry = $"Archivo: {Path.GetFileName(nombreArchivo)} - Fecha: {DateTime.Now:G}{Environment.NewLine}";
                File.AppendAllText(archivoLog, logEntry);

                currentFile++;

                // Actualizar el archivo de estado cada 50 archivos
                if (i % 50 == 0 || i == totalArchivos - 1)
                {
                    var tempStatus = new { currentFile, totalFiles = totalArchivos, completed = false };
                    File.WriteAllText(archivoStatus, JsonSerializer.Serialize(tempStatus));
                }
            }
        });
    });

    // Actualizar el estado al finalizar
    stopwatch.Stop();
    status = new { currentFile = totalArchivos, totalFiles = totalArchivos, completed = true };
    await File.WriteAllTextAsync(archivoStatus, JsonSerializer.Serialize(status));

    TimeSpan tiempoTranscurrido = stopwatch.Elapsed;

    // Construir la respuesta
    var response = new
    {
        Mensaje = "¡Archivos generados exitosamente!",
        TiempoTranscurrido = $"{tiempoTranscurrido.TotalSeconds:F2}s",
        Logs = File.ReadAllLines(archivoLog),
        EstadoFinal = status
    };

    // Devolver la respuesta como JSON
    return Results.Json(response);
});

// Endpoint para escribir operaciones matemáticas en formato JSON
app.MapPost("/write-math-operations", async (HttpContext httpContext) =>
{
    string carpetaDestino = Path.Combine(Directory.GetCurrentDirectory(), "Archivos");
    string statusFilePath = Path.Combine(Directory.GetCurrentDirectory(), "data", "status_process.json");

    if (!Directory.Exists(carpetaDestino))
        return Results.Json(new { Error = "La carpeta de archivos no existe. Genere los archivos primero." });

    // Obtener todos los archivos .txt excepto log.txt
    var archivos = Directory.GetFiles(carpetaDestino, "*.txt")
                             .Where(archivo => !Path.GetFileName(archivo).Equals("log.txt", StringComparison.OrdinalIgnoreCase))
                             .ToArray();

    if (archivos.Length == 0)
        return Results.Json(new { Error = "No hay archivos disponibles para escribir las operaciones matemáticas." });

    // Inicializar el archivo de estado si no existe
    if (!File.Exists(statusFilePath))
    {
        var initialStatus = new
        {
            currentFile = 0,
            currentOperation = 0,
            totalOperations = archivos.Length * 5000,
            completed = false
        };
        await File.WriteAllTextAsync(statusFilePath, JsonSerializer.Serialize(initialStatus, new JsonSerializerOptions { WriteIndented = true }));
    }

    object lockObject = new object();
    int totalOperacionesPorArchivo = 5000;
    int archivosProcesados = 0;

    Stopwatch stopwatch = Stopwatch.StartNew();

    await Task.Run(() =>
    {
        Parallel.ForEach(archivos, (archivo, state, index) =>
        {
            var operaciones = new List<object>();
            for (int i = 1; i <= totalOperacionesPorArchivo; i++)
            {
                operaciones.Add(new
                {
                    operacion = $"({i} * 157.89 / 23.45 - 789.12 * 34.56 + {i})",
                    numero = i
                });
            }

            // Escribir el JSON en el archivo .txt
            var jsonContent = new { operaciones };
            File.WriteAllText(archivo, JsonSerializer.Serialize(jsonContent, new JsonSerializerOptions { WriteIndented = true }));

            // Incrementar el contador de archivos procesados
            archivosProcesados++;

            if (archivosProcesados % 50 == 0)
            {
                // Actualizar "currentFile" y "currentOperation" en el archivo de estado cada 50 archivos
                lock (lockObject)
                {
                    string statusContent = File.ReadAllText(statusFilePath);
                    var status = JsonSerializer.Deserialize<JsonElement>(statusContent);

                    // Obtener los valores de JsonElement y convertir a int
                    int currentFile = status.GetProperty("currentFile").GetInt32();
                    int currentOperation = status.GetProperty("currentOperation").GetInt32();

                    currentFile++;
                    currentOperation += totalOperacionesPorArchivo;

                    var updatedStatus = new
                    {
                        currentFile = currentFile,
                        currentOperation = currentOperation,
                        totalOperations = archivos.Length * totalOperacionesPorArchivo,
                        completed = false
                    };

                    // Escribir el estado actualizado al archivo
                    File.WriteAllText(statusFilePath, JsonSerializer.Serialize(updatedStatus, new JsonSerializerOptions { WriteIndented = true }));
                }
            }

        });

        // Al finalizar, actualizar "completed" en el archivo de estado
        lock (lockObject)
        {
            string statusContent = File.ReadAllText(statusFilePath);
            var status = JsonSerializer.Deserialize<JsonElement>(statusContent);

            var updatedStatus = new
            {
                currentFile = status.GetProperty("currentFile").GetInt32(),
                currentOperation = status.GetProperty("currentOperation").GetInt32(),
                totalOperations = archivos.Length * totalOperacionesPorArchivo,
                completed = true
            };

            // Escribir el estado actualizado al archivo
            File.WriteAllText(statusFilePath, JsonSerializer.Serialize(updatedStatus, new JsonSerializerOptions { WriteIndented = true }));
        }
    });

    stopwatch.Stop();
    TimeSpan tiempoTranscurrido = stopwatch.Elapsed;

    var response = new
    {
        Mensaje = "¡Operaciones matemáticas escritas exitosamente!",
        TiempoTranscurrido = $"{tiempoTranscurrido.TotalSeconds:F2}s",
        TotalOperaciones = archivos.Length * totalOperacionesPorArchivo,
        TotalArchivos = archivos.Length
    };

    return Results.Json(response);
});

app.MapGet("/process-math-operations", async (HttpContext httpContext) =>
{
    // Ruta de la carpeta donde están los archivos
    string carpetaDestino = Path.Combine(Directory.GetCurrentDirectory(), "Archivos");

    // Verifica que la carpeta exista
    if (!Directory.Exists(carpetaDestino))
        return Results.Json(new { Error = "La carpeta de archivos no existe. Genere los archivos primero." });

    // Obtiene todos los archivos .txt excepto el log
    var archivos = Directory.GetFiles(carpetaDestino, "*.txt")
                             .Where(archivo => !Path.GetFileName(archivo).Equals("log.txt", StringComparison.OrdinalIgnoreCase))
                             .ToArray();

    if (archivos.Length == 0)
        return Results.Json(new { Error = "No hay archivos disponibles para procesar las operaciones matemáticas." });

    double sumaTotal = 0;
    object lockObject = new object();
    Stopwatch stopwatch = Stopwatch.StartNew();

    // Procesar archivos de forma paralela
    await Task.Run(() =>
    {
        Parallel.ForEach(archivos, archivo =>
        {
            try
            {
                // Leer el contenido del archivo
                var contenido = File.ReadAllText(archivo);

                // Deserializar directamente como JSON dinámico
                using var jsonDoc = JsonDocument.Parse(contenido);
                var operaciones = jsonDoc.RootElement.GetProperty("operaciones");

                double sumaArchivo = 0;

                foreach (var operacion in operaciones.EnumerateArray())
                {
                    // Extraer los valores de operación y número
                    string expresion = operacion.GetProperty("operacion").GetString() ?? string.Empty;
                    int numero = operacion.GetProperty("numero").GetInt32();

                    if (!string.IsNullOrWhiteSpace(expresion))
                    {
                        // Reemplazar N por el valor del número en la expresión
                        string expresionEvaluada = expresion.Replace($"(N", $"({numero}.0");

                        // Evaluar la expresión matemáticamente
                        double resultado = EvaluarExpresion(expresionEvaluada);
                        sumaArchivo += resultado;
                    }
                }

                // Agregar la suma de este archivo al total
                lock (lockObject)
                {
                    sumaTotal += sumaArchivo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando el archivo {archivo}: {ex.Message}");
            }
        });
    });

    stopwatch.Stop();
    TimeSpan tiempoTranscurrido = stopwatch.Elapsed;

    // Respuesta de la API
    var response = new
    {
        Mensaje = "¡Operaciones matemáticas procesadas exitosamente!",
        SumaTotal = sumaTotal,
        TiempoTranscurrido = $"{tiempoTranscurrido.TotalSeconds:F2}s",
        TotalArchivosProcesados = archivos.Length
    };

    return Results.Json(response);
});

// Método para evaluar expresiones matemáticas
double EvaluarExpresion(string expresion)
{
    try
    {
        var dataTable = new System.Data.DataTable();
        var resultado = dataTable.Compute(expresion, null);
        return Convert.ToDouble(resultado);
    }
    catch
    {
        return 0; // Si hay algún error, devolvemos 0
    }
}



app.Run();
