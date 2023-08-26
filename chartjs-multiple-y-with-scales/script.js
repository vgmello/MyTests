const ctx = document.getElementById('myChart');

const properties = ['Sales', 'Purchases', 'Users'];
const janData = [5, 100, 1000];
const febData = [10, 400, 10000];

const janColor = 'rgba(255, 99, 132, 0.2)';
const febColor = 'rgba(75, 192, 192, 0.2)';

const generateDataset = (properties, monthName, monthData, color) => {
  return properties.map((prop, propIndex) => ({
    label: `${monthName} ${prop}`,
    data: properties.map((_, dataIndex) =>
      dataIndex === propIndex ? monthData[dataIndex] : null
    ),
    backgroundColor: color,
    yAxisID: `y-${prop.toLowerCase()}`,
    stack: `${monthName}`,
  }));
};

const generateScale = (properties) => {
  let scales = {};
  properties.forEach((prop, propIndex) => {
    scales[`y-${prop.toLowerCase()}`] = {
      // min: 0,
      // max: getMaxValueForProperty(index) * 1.2,
      display: false,
    };
  });
  return scales;
};

const data = {
  labels: properties,
  datasets: [
    ...generateDataset(properties, 'Jan', janData, janColor),
    ...generateDataset(properties, 'Feb', febData, febColor),
  ],
};

const config = {
  type: 'bar',
  data: data,
  options: {
    scales: generateScale(properties),
    responsive: true,
    plugins: {
      legend: {
        position: 'top',
        labels: {
          generateLabels: () => [
            {
              text: 'Jan',
              fillStyle: janColor,
              strokeStyle: 'rgba(0, 0, 0, 0.1)',
            },
            {
              text: 'Feb',
              fillStyle: febColor,
              strokeStyle: 'rgba(0, 0, 0, 0.1)',
            },
          ],
        },
      },
      datalabels: {
        display: true,
        align: 'center',
        anchor: 'center',
      },
      title: {
        display: true,
        text: 'Chart.js Bar Chart',
      },
    },
  },
};

Chart.register(ChartDataLabels);

new Chart(ctx, config);
