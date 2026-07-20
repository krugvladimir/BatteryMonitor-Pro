// batterymonitor_cpp.cpp — монитор батареи с графиками на C++ (Qt Charts)

#include <QApplication>
#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QTimer>
#include <QChartView>
#include <QLineSeries>
#include <QValueAxis>
#include <QChart>
#include <QMessageBox>
#include <QFile>
#include <QTextStream>
#include <QDateTime>
#include <random>
#include <deque>

class BatteryMonitor : public QMainWindow {
    Q_OBJECT
public:
    BatteryMonitor(QWidget *parent = nullptr) : QMainWindow(parent) {
        setWindowTitle("🔋 BatteryMonitor Pro — C++");
        resize(800, 600);

        // Данные
        chargeHistory.resize(60);
        tempHistory.resize(60);
        timeHistory.resize(60);

        // Настройка графика
        chart = new QtCharts::QChart();
        chart->setTitle("Состояние батареи");
        chart->legend()->hide();
        chart->setAnimationOptions(QtCharts::QChart::SeriesAnimations);

        chargeSeries = new QtCharts::QLineSeries();
        chargeSeries->setName("Заряд");
        chargeSeries->setColor(Qt::blue);

        tempSeries = new QtCharts::QLineSeries();
        tempSeries->setName("Температура");
        tempSeries->setColor(Qt::red);

        // Оси
        axisX = new QtCharts::QValueAxis();
        axisX->setTitleText("Время (сек)");
        axisX->setRange(0, 60);
        axisX->setLabelFormat("%d");

        axisY = new QtCharts::QValueAxis();
        axisY->setTitleText("Заряд (%)");
        axisY->setRange(0, 105);

        axisY2 = new QtCharts::QValueAxis();
        axisY2->setTitleText("Температура (°C)");
        axisY2->setRange(0, 60);
        axisY2->setLabelsColor(Qt::red);

        chart->addSeries(chargeSeries);
        chart->addSeries(tempSeries);
        chart->setAxisX(axisX, chargeSeries);
        chart->setAxisY(axisY, chargeSeries);
        chart->setAxisX(axisX, tempSeries);
        chart->setAxisY(axisY2, tempSeries);

        chartView = new QtCharts::QChartView(chart);
        chartView->setRenderHint(QPainter::Antialiasing);

        // UI
        QWidget *central = new QWidget(this);
        setCentralWidget(central);
        QVBoxLayout *mainLayout = new QVBoxLayout(central);

        // Информация
        QHBoxLayout *infoLayout = new QHBoxLayout();
        chargeLabel = new QLabel("Заряд: 100%");
        timeLabel = new QLabel("Время: --");
        tempLabel = new QLabel("Температура: 25°C");
        statusLabel = new QLabel("Статус: Норма");
        infoLayout->addWidget(chargeLabel);
        infoLayout->addWidget(timeLabel);
        infoLayout->addWidget(tempLabel);
        infoLayout->addWidget(statusLabel);
        mainLayout->addLayout(infoLayout);

        // График
        mainLayout->addWidget(chartView);

        // Кнопки
        QHBoxLayout *btnLayout = new QHBoxLayout();
        QPushButton *exportBtn = new QPushButton("Экспорт CSV");
        QPushButton *resetBtn = new QPushButton("Сбросить данные");
        btnLayout->addWidget(exportBtn);
        btnLayout->addWidget(resetBtn);
        mainLayout->addLayout(btnLayout);

        connect(exportBtn, &QPushButton::clicked, this, &BatteryMonitor::exportCSV);
        connect(resetBtn, &QPushButton::clicked, this, &BatteryMonitor::resetData);

        // Таймер обновления
        timer = new QTimer(this);
        connect(timer, &QTimer::timeout, this, &BatteryMonitor::updateData);
        timer->start(1000);

        // Инициализация симуляции
        currentCharge = 100.0;
        currentTemp = 25.0;
        notifiedLow = false;
        notifiedHighTemp = false;
        gen = std::mt19937(rd());
        dist = std::uniform_real_distribution<>(-0.5, 0.1);
        tempDist = std::uniform_real_distribution<>(-2, 5);
    }

private slots:
    void updateData() {
        // Симуляция
        currentCharge += dist(gen);
        if (currentCharge < 0) currentCharge = 0;
        if (currentCharge > 100) currentCharge = 100;
        currentTemp += tempDist(gen) + (100 - currentCharge) * 0.1;
        if (currentTemp < 20) currentTemp = 20;
        if (currentTemp > 60) currentTemp = 60;

        // Расчёт времени
        if (chargeHistory.size() > 5) {
            double rate = (chargeHistory.back() - chargeHistory.front()) / chargeHistory.size();
            if (rate < 0) {
                remainingTime = static_cast<int>(currentCharge / abs(rate) * 60);
            } else {
                remainingTime = 999;
            }
        } else {
            remainingTime = 999;
        }

        // Добавляем в историю
        chargeHistory.push_back(currentCharge);
        tempHistory.push_back(currentTemp);
        timeHistory.push_back(QDateTime::currentSecsSinceEpoch());
        if (chargeHistory.size() > 60) {
            chargeHistory.pop_front();
            tempHistory.pop_front();
            timeHistory.pop_front();
        }

        // Обновление графиков
        chargeSeries->clear();
        tempSeries->clear();
        for (int i = 0; i < chargeHistory.size(); ++i) {
            chargeSeries->append(i, chargeHistory[i]);
            tempSeries->append(i, tempHistory[i]);
        }

        // Обновление меток
        chargeLabel->setText(QString("Заряд: %1%").arg(currentCharge, 0, 'f', 1));
        if (remainingTime < 999) {
            int hours = remainingTime / 60;
            int mins = remainingTime % 60;
            timeLabel->setText(QString("Время: %1ч %2мин").arg(hours).arg(mins));
        } else {
            timeLabel->setText("Время: ∞");
        }
        tempLabel->setText(QString("Температура: %1°C").arg(currentTemp, 0, 'f', 1));

        // Статус
        if (currentCharge > 80) {
            statusLabel->setText("Статус: Отлично");
            statusLabel->setStyleSheet("color: green;");
        } else if (currentCharge > 50) {
            statusLabel->setText("Статус: Хорошо");
            statusLabel->setStyleSheet("color: blue;");
        } else if (currentCharge > 20) {
            statusLabel->setText("Статус: Нормально");
            statusLabel->setStyleSheet("color: orange;");
        } else {
            statusLabel->setText("Статус: Критично!");
            statusLabel->setStyleSheet("color: red;");
        }

        // Уведомления
        if (currentCharge < 20 && !notifiedLow) {
            notifiedLow = true;
            QMessageBox::warning(this, "Низкий заряд", QString("Уровень батареи %1%! Подключите зарядку.").arg(currentCharge, 0, 'f', 1));
        } else if (currentCharge >= 25) {
            notifiedLow = false;
        }
        if (currentTemp > 50 && !notifiedHighTemp) {
            notifiedHighTemp = true;
            QMessageBox::warning(this, "Высокая температура", QString("Температура батареи %1°C! Охладите устройство.").arg(currentTemp, 0, 'f', 1));
        } else if (currentTemp <= 45) {
            notifiedHighTemp = false;
        }
    }

    void exportCSV() {
        if (chargeHistory.size() < 2) {
            QMessageBox::information(this, "Информация", "Недостаточно данных для экспорта");
            return;
        }
        QString filename = QString("battery_data_%1.csv").arg(QDateTime::currentSecsSinceEpoch());
        QFile file(filename);
        if (file.open(QIODevice::WriteOnly | QIODevice::Text)) {
            QTextStream out(&file);
            out << "Время,Заряд(%),Температура(°C)\n";
            for (int i = 0; i < chargeHistory.size(); ++i) {
                out << timeHistory[i] << "," << chargeHistory[i] << "," << tempHistory[i] << "\n";
            }
            file.close();
            QMessageBox::information(this, "Экспорт", "Данные сохранены в " + filename);
        }
    }

    void resetData() {
        chargeHistory.clear();
        tempHistory.clear();
        timeHistory.clear();
        currentCharge = 100;
        currentTemp = 25;
        remainingTime = 0;
        chargeSeries->clear();
        tempSeries->clear();
    }

private:
    std::deque<double> chargeHistory, tempHistory;
    std::deque<qint64> timeHistory;
    double currentCharge, currentTemp;
    int remainingTime;
    bool notifiedLow, notifiedHighTemp;

    QLabel *chargeLabel, *timeLabel, *tempLabel, *statusLabel;
    QtCharts::QChartView *chartView;
    QtCharts::QChart *chart;
    QtCharts::QLineSeries *chargeSeries, *tempSeries;
    QtCharts::QValueAxis *axisX, *axisY, *axisY2;
    QTimer *timer;

    std::random_device rd;
    std::mt19937 gen;
    std::uniform_real_distribution<> dist, tempDist;
};

int main(int argc, char *argv[]) {
    QApplication app(argc, argv);
    BatteryMonitor w;
    w.show();
    return app.exec();
}

#include "batterymonitor_cpp.moc"
