// Donut Chart Manipulation
function addDonutChartLabel(chartId, donutColor, textData) {
    var gNum = 4;
    if (chartId == "#donut2" || chartId == "#donut3") gNum = 3
    // access text element within chart
    var selection = chartId + " > div > div:nth-child(1) > div > svg > g:nth-child(" + gNum + ") > text"
    var chartTextElement = document.querySelector(selection)
    // grab the chart div
    var donutChart = document.getElementById(chartId.substring(1))

    donutChart.appendChild(createLabel("donutChartLabel", donutColor, textData))
}
function createLabel(name, color, text) {
    var newElement = document.createElement("span")
    newElement.className = name
    newElement.style.color = color
    newElement.textContent = text

    return newElement;
}