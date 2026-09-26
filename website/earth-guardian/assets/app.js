'use strict';
// The controls below are a website demo only. No system memory APIs are used.
const globe = document.querySelector('#planet-demo');
const globeState = document.querySelector('#planet-state');
const demoCaption = document.querySelector('#demo-caption');
globe.disabled = false;
globe.addEventListener('click', () => {
  globe.disabled = true;
  globe.classList.add('is-cleaning');
  globeState.textContent = '清理动画演示';
  demoCaption.textContent = '正在体验交互动画 · 不会执行本机内存清理';
  window.setTimeout(() => {
    globe.classList.remove('is-cleaning');
    globe.disabled = false;
    globeState.textContent = '再次体验';
    demoCaption.textContent = '演示完成 · 数值为示例；实际清理请使用 Windows 客户端';
  }, 2000);
});
const autoMode = document.querySelector('#auto-preview');
const threshold = document.querySelector('#threshold-preview');
const thresholdValue = document.querySelector('#threshold-value');
const thresholdDescription = document.querySelector('#threshold-description');
function updatePreview() {
  thresholdValue.value = threshold.value + '%';
  threshold.disabled = !autoMode.checked;
  thresholdDescription.textContent = autoMode.checked
    ? '达到 ' + threshold.value + '% 并持续 30 秒后清理'
    : '自动模式已关闭 · 此处仅为网页设置体验';
}
autoMode.addEventListener('change', updatePreview);
threshold.addEventListener('input', updatePreview);
updatePreview();
const pressure = document.querySelector('#pressure-preview');
const pressureEarth = document.querySelector('#pressure-earth');
function updatePressure() {
  const load = Number(pressure.value);
  const heat = Math.max(0, Math.min(1, (load - 50) / 45));
  const tint = 'hue-rotate(' + (-190 * heat) + 'deg)';
  pressureEarth.style.filter = tint;
  document.querySelector('.planet').style.filter = tint + ' drop-shadow(0 0 25px #3eb6ca24)';
  document.querySelector('.planet-percent').innerHTML = load + '<span>%</span>';
  document.querySelector('#pressure-value').value = load + '%';
  pressureEarth.alt = '内存占用 ' + load + '% 的地球变色示意';
}
pressure.addEventListener('input', updatePressure);
updatePressure();
document.querySelector('.copy-name').addEventListener('click', async function () {
  const message = document.querySelector('#copy-status');
  try {
    if (!navigator.clipboard) throw new Error('Clipboard unavailable');
    await navigator.clipboard.writeText('宋域强');
    this.textContent = '已复制 ✓';
    message.textContent = '已复制宋域强，可在微信或抖音搜索。';
  } catch (_) {
    this.textContent = '请手动复制：宋域强';
    message.textContent = '请手动选择并复制名字：宋域强。';
  }
});
