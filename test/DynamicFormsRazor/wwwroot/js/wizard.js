(function(){
  function showStep(idx){
    const steps = document.querySelectorAll('.wizard-step');
    const buttons = document.querySelectorAll('.wizard-step-button');
    steps.forEach((s,i)=>{
      s.style.display = (i === idx) ? 'block' : 'none';
    });
    buttons.forEach((b,i)=>{
      b.classList.toggle('active', i===idx);
    });
    const prev = document.getElementById('prevStep');
    const next = document.getElementById('nextStep');
    const submit = document.getElementById('submitBtn');
    if(!prev || !next || !submit) return;
    prev.disabled = (idx === 0);
    const last = (idx === steps.length - 1);
    next.style.display = last ? 'none' : 'inline-block';
    submit.style.display = last ? 'inline-block' : 'none';
    window.__wizardIndex = idx;
  }

  document.addEventListener('DOMContentLoaded', function(){
    window.__wizardIndex = 0;
    showStep(0);
    const buttons = document.querySelectorAll('.wizard-step-button');
    buttons.forEach((b)=>{
      b.addEventListener('click', ()=>{
        const idx = parseInt(b.getAttribute('data-step'), 10);
        showStep(idx);
      });
    });
    const prev = document.getElementById('prevStep');
    const next = document.getElementById('nextStep');
    if(prev) prev.addEventListener('click', ()=> showStep(Math.max(0, (window.__wizardIndex||0)-1)));
    if(next) next.addEventListener('click', ()=> {
      // basic required field check within current step (client-side)
      const step = document.querySelector('.wizard-step[style*="block"]');
      if(step){
        const requiredInputs = step.querySelectorAll('[data-required="true"], [required]');
        for(const el of requiredInputs){
          if((el.type === 'checkbox' && !el.checked) || !el.value){
            el.focus();
            return;
          }
        }
      }
      showStep((window.__wizardIndex||0)+1);
    });
  });
})();
