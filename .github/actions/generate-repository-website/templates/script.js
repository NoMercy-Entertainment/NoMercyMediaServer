// Tab switching functionality
function switchTab(tabName) {
  // Hide all tab contents
  const contents = document.querySelectorAll('.tab-content');
  contents.forEach(content => content.classList.remove('active'));
  
  // Remove active class from all tabs
  const tabs = document.querySelectorAll('.nav-tab');
  tabs.forEach(tab => tab.classList.remove('active'));
  
  // Show selected tab content
  document.getElementById(tabName).classList.add('active');
  
  // Add active class to clicked tab
  event.target.classList.add('active');
  
  // Update URL hash
  window.location.hash = tabName;
}

// Copy to clipboard functionality
function copyToClipboard(button) {
  const codeBlock = button.nextElementSibling;
  const text = codeBlock.textContent;
  
  navigator.clipboard.writeText(text).then(() => {
    const originalText = button.textContent;
    button.textContent = 'Copied!';
    button.style.background = '#48bb78';
    
    setTimeout(() => {
      button.textContent = originalText;
      button.style.background = '#4a5568';
    }, 2000);
  }).catch(err => {
    console.error('Failed to copy text: ', err);
    button.textContent = 'Failed';
    setTimeout(() => {
      button.textContent = 'Copy';
    }, 2000);
  });
}

// Initialize page
document.addEventListener('DOMContentLoaded', function() {
  // Check URL hash and activate appropriate tab
  const hash = window.location.hash.substring(1);
  if (hash && ['ubuntu', 'fedora', 'arch'].includes(hash)) {
    switchTab(hash);
  }
  
  // Add smooth scrolling for better UX
  document.querySelectorAll('a[href^="#"]').forEach(anchor => {
    anchor.addEventListener('click', function (e) {
      e.preventDefault();
      const target = document.querySelector(this.getAttribute('href'));
      if (target) {
        target.scrollIntoView({
          behavior: 'smooth'
        });
      }
    });
  });

  
    const year = document.querySelector('#year');
    year.textContent = new Date().getFullYear();
});
