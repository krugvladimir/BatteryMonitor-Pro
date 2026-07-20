// batterymonitor_java.java — монитор батареи с графиками на Java (Swing)

import javax.swing.*;
import java.awt.*;
import java.awt.event.*;
import java.awt.geom.*;
import java.util.*;
import java.util.List;
import java.io.*;
import java.nio.file.*;
import java.time.*;

public class BatteryMonitorJava extends JFrame {
    private static final int HISTORY_SIZE = 60;
    private Deque<Double> chargeHistory = new ArrayDeque<>(HISTORY_SIZE);
    private Deque<Double> tempHistory = new ArrayDeque<>(HISTORY_SIZE);
    private Deque<Long> timeHistory = new ArrayDeque<>(HISTORY_SIZE);
    private double currentCharge = 100.0;
    private double currentTemp = 25.0;
    private int remainingTime = 0;
    private boolean notifiedLow = false, notifiedHighTemp = false;
    private Timer timer;
    private Random rand = new Random();

    private JLabel chargeLabel, timeLabel, tempLabel, statusLabel;
    private GraphPanel graphPanel;

    public BatteryMonitorJava() {
        setTitle("🔋 BatteryMonitor Pro — Java");
        setSize(800, 600);
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setLayout(new BorderLayout());

        // Info panel
        JPanel infoPanel = new JPanel(new FlowLayout());
        chargeLabel = new JLabel("Заряд: 100%");
        timeLabel = new JLabel("Время: --");
        tempLabel = new JLabel("Температура: 25°C");
        statusLabel = new JLabel("Статус: Норма");
        statusLabel.setForeground(Color.GREEN);
        infoPanel.add(chargeLabel);
        infoPanel.add(timeLabel);
        infoPanel.add(tempLabel);
        infoPanel.add(statusLabel);
        add(infoPanel, BorderLayout.NORTH);

        // Graph
        graphPanel = new GraphPanel();
        add(graphPanel, BorderLayout.CENTER);

        // Buttons
        JPanel btnPanel = new JPanel();
        JButton exportBtn = new JButton("Экспорт CSV");
        JButton resetBtn = new JButton("Сбросить данные");
        btnPanel.add(exportBtn);
        btnPanel.add(resetBtn);
        add(btnPanel, BorderLayout.SOUTH);

        exportBtn.addActionListener(e -> exportCSV());
        resetBtn.addActionListener(e -> resetData());

        // Timer
        timer = new Timer(1000, e -> updateData());
        timer.start();

        // Initial data
        for (int i = 0; i < 10; i++) {
            chargeHistory.add(currentCharge);
            tempHistory.add(currentTemp);
            timeHistory.add(System.currentTimeMillis()/1000);
        }
    }

    private void updateData() {
        // Симуляция
        currentCharge += rand.nextDouble() * 0.6 - 0.5;
        currentCharge = Math.max(0, Math.min(100, currentCharge));
        currentTemp += rand.nextDouble() * 7 - 2 + (100 - currentCharge) * 0.1;
        currentTemp = Math.max(20, Math.min(60, currentTemp));

        // Время
        if (chargeHistory.size() > 5) {
            double[] arr = chargeHistory.stream().mapToDouble(Double::doubleValue).toArray();
            double rate = (arr[arr.length-1] - arr[0]) / arr.length;
            if (rate < 0) {
                remainingTime = (int)(currentCharge / Math.abs(rate) * 60);
            } else {
                remainingTime = 999;
            }
        }

        // История
        chargeHistory.addLast(currentCharge);
        tempHistory.addLast(currentTemp);
        timeHistory.addLast(System.currentTimeMillis()/1000);
        if (chargeHistory.size() > HISTORY_SIZE) {
            chargeHistory.removeFirst();
            tempHistory.removeFirst();
            timeHistory.removeFirst();
        }

        // Обновление GUI
        SwingUtilities.invokeLater(() -> {
            chargeLabel.setText(String.format("Заряд: %.1f%%", currentCharge));
            if (remainingTime < 999) {
                timeLabel.setText(String.format("Время: %dч %dмин", remainingTime/60, remainingTime%60));
            } else {
                timeLabel.setText("Время: ∞");
            }
            tempLabel.setText(String.format("Температура: %.1f°C", currentTemp));

            if (currentCharge > 80) {
                statusLabel.setText("Статус: Отлично");
                statusLabel.setForeground(Color.GREEN);
            } else if (currentCharge > 50) {
                statusLabel.setText("Статус: Хорошо");
                statusLabel.setForeground(Color.BLUE);
            } else if (currentCharge > 20) {
                statusLabel.setText("Статус: Нормально");
                statusLabel.setForeground(Color.ORANGE);
            } else {
                statusLabel.setText("Статус: Критично!");
                statusLabel.setForeground(Color.RED);
            }

            graphPanel.repaint();

            // Уведомления
            if (currentCharge < 20 && !notifiedLow) {
                notifiedLow = true;
                JOptionPane.showMessageDialog(this, String.format("Уровень батареи %.1f%%! Подключите зарядку.", currentCharge), "Низкий заряд", JOptionPane.WARNING_MESSAGE);
            } else if (currentCharge >= 25) {
                notifiedLow = false;
            }
            if (currentTemp > 50 && !notifiedHighTemp) {
                notifiedHighTemp = true;
                JOptionPane.showMessageDialog(this, String.format("Температура батареи %.1f°C! Охладите устройство.", currentTemp), "Высокая температура", JOptionPane.WARNING_MESSAGE);
            } else if (currentTemp <= 45) {
                notifiedHighTemp = false;
            }
        });
    }

    private void exportCSV() {
        if (chargeHistory.size() < 2) {
            JOptionPane.showMessageDialog(this, "Недостаточно данных для экспорта");
            return;
        }
        String filename = String.format("battery_data_%d.csv", System.currentTimeMillis()/1000);
        try (PrintWriter pw = new PrintWriter(new File(filename))) {
            pw.println("Время,Заряд(%),Температура(°C)");
            Iterator<Double> chargeIt = chargeHistory.iterator();
            Iterator<Double> tempIt = tempHistory.iterator();
            Iterator<Long> timeIt = timeHistory.iterator();
            while (chargeIt.hasNext()) {
                pw.printf("%d,%.2f,%.2f\n", timeIt.next(), chargeIt.next(), tempIt.next());
            }
            JOptionPane.showMessageDialog(this, "Данные сохранены в " + filename);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    private void resetData() {
        chargeHistory.clear();
        tempHistory.clear();
        timeHistory.clear();
        currentCharge = 100;
        currentTemp = 25;
        remainingTime = 0;
        // Заполняем начальными данными
        for (int i = 0; i < 10; i++) {
            chargeHistory.add(currentCharge);
            tempHistory.add(currentTemp);
            timeHistory.add(System.currentTimeMillis()/1000);
        }
        graphPanel.repaint();
    }

    class GraphPanel extends JPanel {
        @Override
        protected void paintComponent(Graphics g) {
            super.paintComponent(g);
            Graphics2D g2 = (Graphics2D) g;
            g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON);

            int w = getWidth();
            int h = getHeight();
            int margin = 40;

            // Оси
            g2.setColor(Color.BLACK);
            g2.drawLine(margin, margin, margin, h-margin);
            g2.drawLine(margin, h-margin, w-margin, h-margin);

            // Подписи осей
            g2.drawString("Время (сек)", w/2-30, h-5);
            g2.drawString("Заряд (%)", 10, margin-10);

            if (chargeHistory.size() < 2) return;

            // Масштабирование
            double maxCharge = 105;
            double minCharge = 0;
            double maxTemp = 60;
            double minTemp = 0;

            int n = chargeHistory.size();
            double stepX = (double)(w - 2*margin) / (n-1);
            double scaleY = (double)(h - 2*margin) / (maxCharge - minCharge);
            double scaleY2 = (double)(h - 2*margin) / (maxTemp - minTemp);

            // Рисуем сетку
            g2.setColor(Color.LIGHT_GRAY);
            for (int i = 0; i <= 5; i++) {
                int y = h - margin - (int)(i * (h-2*margin) / 5);
                g2.drawLine(margin, y, w-margin, y);
                g2.drawString(String.valueOf((int)(i*20)), 5, y+4);
            }

            // График заряда
            g2.setColor(Color.BLUE);
            double[] chargeArr = chargeHistory.stream().mapToDouble(Double::doubleValue).toArray();
            int i = 0;
            for (Double val : chargeHistory) {
                int x = margin + (int)(i * stepX);
                int y = h - margin - (int)((val - minCharge) * scaleY);
                if (i == 0) {
                    g2.fillOval(x-3, y-3, 6, 6);
                } else {
                    int prevX = margin + (int)((i-1) * stepX);
                    int prevY = h - margin - (int)((chargeArr[i-1] - minCharge) * scaleY);
                    g2.drawLine(prevX, prevY, x, y);
                    g2.fillOval(x-3, y-3, 6, 6);
                }
                i++;
            }

            // График температуры (красный)
            g2.setColor(Color.RED);
            i = 0;
            for (Double val : tempHistory) {
                int x = margin + (int)(i * stepX);
                int y = h - margin - (int)((val - minTemp) * scaleY2);
                if (i == 0) {
                    g2.fillOval(x-3, y-3, 6, 6);
                } else {
                    int prevX = margin + (int)((i-1) * stepX);
                    int prevY = h - margin - (int)((tempHistory.toArray(new Double[0])[i-1] - minTemp) * scaleY2);
                    g2.drawLine(prevX, prevY, x, y);
                    g2.fillOval(x-3, y-3, 6, 6);
                }
                i++;
            }

            // Легенда
            g2.setColor(Color.BLUE);
            g2.drawString("Заряд", w-60, margin+20);
            g2.setColor(Color.RED);
            g2.drawString("Температура", w-60, margin+40);
        }
    }

    public static void main(String[] args) throws Exception {
        UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
        SwingUtilities.invokeLater(() -> new BatteryMonitorJava().setVisible(true));
    }
}
