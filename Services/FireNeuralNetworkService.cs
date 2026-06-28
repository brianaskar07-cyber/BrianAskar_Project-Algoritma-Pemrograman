namespace FIRE;

public class NeuralFireResult
{
    public double RiskScore { get; set; }
    public string Status { get; set; } = "";
    public string Explanation { get; set; } = "";
}

public class FireNeuralNetworkService
{
    private const int InputCount = 3;
    private const int HiddenCount = 4;

    private readonly double[,] _weightInputHidden = new double[InputCount, HiddenCount];
    private readonly double[] _biasHidden = new double[HiddenCount];

    private readonly double[] _weightHiddenOutput = new double[HiddenCount];
    private double _biasOutput;

    private readonly Random _random = new Random(7);

    public FireNeuralNetworkService()
    {
        InitializeWeights();
        TrainDefaultDataset();
    }

    public NeuralFireResult Predict(int suhu, int asap, int kelembapan)
    {
        double[] input = NormalizeInput(suhu, asap, kelembapan);

        double[] hidden = CalculateHiddenLayer(input);
        double output = CalculateOutputLayer(hidden);

        double riskScore = output * 100.0;

        string status;

        if (riskScore >= 75)
            status = "BAHAYA";
        else if (riskScore >= 45)
            status = "WASPADA";
        else
            status = "AMAN";

        string explanation = BuildExplanation(suhu, asap, kelembapan, riskScore, status);

        return new NeuralFireResult
        {
            RiskScore = riskScore,
            Status = status,
            Explanation = explanation
        };
    }

    public string PredictToText(int suhu, int asap, int kelembapan)
    {
        NeuralFireResult result = Predict(suhu, asap, kelembapan);

        return
            $"Analisis Neural Network:\n\n" +
            $"- Input suhu: {suhu}°C\n" +
            $"- Input asap: {asap} ppm\n" +
            $"- Input kelembapan: {kelembapan}%\n\n" +
            $"Metode:\n" +
            $"- Input layer: 3 neuron\n" +
            $"- Hidden layer: 4 neuron\n" +
            $"- Output layer: 1 neuron risiko kebakaran\n" +
            $"- Proses: feedforward dan backpropagation\n\n" +
            $"Hasil prediksi risiko: {result.RiskScore:F1}%\n" +
            $"Status NN: {result.Status}\n\n" +
            $"{result.Explanation}";
    }

    private void InitializeWeights()
    {
        for (int i = 0; i < InputCount; i++)
        {
            for (int j = 0; j < HiddenCount; j++)
            {
                _weightInputHidden[i, j] = RandomWeight();
            }
        }

        for (int j = 0; j < HiddenCount; j++)
        {
            _biasHidden[j] = RandomWeight();
            _weightHiddenOutput[j] = RandomWeight();
        }

        _biasOutput = RandomWeight();
    }

    private double RandomWeight()
    {
        return (_random.NextDouble() * 2.0) - 1.0;
    }

    private void TrainDefaultDataset()
    {
        var trainingData = new List<(int suhu, int asap, int hum, double target)>
        {
            (25, 100, 80, 0.05),
            (30, 200, 60, 0.15),
            (40, 300, 45, 0.35),
            (50, 450, 45, 0.50),
            (55, 500, 40, 0.60),
            (65, 650, 35, 0.75),
            (70, 350, 30, 0.78),
            (75, 800, 30, 0.95),
            (85, 900, 25, 0.99),
            (35, 750, 35, 0.65),
            (60, 750, 55, 0.72),
            (45, 250, 80, 0.30)
        };

        double learningRate = 0.08;
        int epochs = 3000;

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            foreach (var sample in trainingData)
            {
                TrainSingleSample(
                    sample.suhu,
                    sample.asap,
                    sample.hum,
                    sample.target,
                    learningRate
                );
            }
        }
    }

    private void TrainSingleSample(
        int suhu,
        int asap,
        int kelembapan,
        double target,
        double learningRate)
    {
        double[] input = NormalizeInput(suhu, asap, kelembapan);

        double[] hidden = CalculateHiddenLayer(input);
        double output = CalculateOutputLayer(hidden);

        // Error output
        double outputError = target - output;

        // Delta output untuk backpropagation
        double deltaOutput = outputError * SigmoidDerivative(output);

        double[] deltaHidden = new double[HiddenCount];

        for (int j = 0; j < HiddenCount; j++)
        {
            deltaHidden[j] =
                hidden[j] *
                (1.0 - hidden[j]) *
                deltaOutput *
                _weightHiddenOutput[j];
        }

        // Update bobot hidden-output
        for (int j = 0; j < HiddenCount; j++)
        {
            _weightHiddenOutput[j] += learningRate * deltaOutput * hidden[j];
        }

        _biasOutput += learningRate * deltaOutput;

        // Update bobot input-hidden
        for (int i = 0; i < InputCount; i++)
        {
            for (int j = 0; j < HiddenCount; j++)
            {
                _weightInputHidden[i, j] += learningRate * deltaHidden[j] * input[i];
            }
        }

        for (int j = 0; j < HiddenCount; j++)
        {
            _biasHidden[j] += learningRate * deltaHidden[j];
        }
    }

    private double[] NormalizeInput(int suhu, int asap, int kelembapan)
    {
        return new[]
        {
            Clamp(suhu / 100.0),
            Clamp(asap / 1000.0),
            Clamp(kelembapan / 100.0)
        };
    }

    private double[] CalculateHiddenLayer(double[] input)
    {
        double[] hidden = new double[HiddenCount];

        for (int j = 0; j < HiddenCount; j++)
        {
            double sum = _biasHidden[j];

            for (int i = 0; i < InputCount; i++)
            {
                sum += input[i] * _weightInputHidden[i, j];
            }

            hidden[j] = Sigmoid(sum);
        }

        return hidden;
    }

    private double CalculateOutputLayer(double[] hidden)
    {
        double sum = _biasOutput;

        for (int j = 0; j < HiddenCount; j++)
        {
            sum += hidden[j] * _weightHiddenOutput[j];
        }

        return Sigmoid(sum);
    }

    private static double Sigmoid(double x)
    {
        return 1.0 / (1.0 + Math.Exp(-x));
    }

    private static double SigmoidDerivative(double sigmoidOutput)
    {
        return sigmoidOutput * (1.0 - sigmoidOutput);
    }

    private static double Clamp(double value)
    {
        if (value < 0)
            return 0;

        if (value > 1)
            return 1;

        return value;
    }

    private static string BuildExplanation(
        int suhu,
        int asap,
        int kelembapan,
        double riskScore,
        string status)
    {
        if (status == "BAHAYA")
        {
            return
                "Penjelasan awam: Neural Network memperkirakan risiko tinggi berdasarkan pola gabungan suhu, asap, dan kelembapan. " +
                "Kondisi ini perlu ditindaklanjuti dengan pemeriksaan lokasi dan persiapan evakuasi.";
        }

        if (status == "WASPADA")
        {
            return
                "Penjelasan awam: Neural Network mendeteksi adanya peningkatan risiko. " +
                "Operator perlu memantau perubahan sensor dan memastikan prosedur keselamatan siap digunakan.";
        }

        return
            "Penjelasan awam: Neural Network memperkirakan risiko kebakaran masih rendah. " +
            "Kondisi tetap perlu dipantau secara berkala.";
    }
}