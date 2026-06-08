const API = window.location.origin + '/api';

document.addEventListener('DOMContentLoaded', () => {
    // Session state
    let parentId = localStorage.getItem('fametra_pid') || null;
    let memberId = localStorage.getItem('fametra_mid') || null;
    
    // Child state
    let memberData = null;
    let totalLimit = 120;
    let timeLeft = 120;
    let timerInterval = null;

    // Parent state
    let parentData = null;
    let generateCodeTimer = null;

    // DOM Elements - Views
    const viewRoleSelect = document.getElementById('view-role-select');
    const viewParentLogin = document.getElementById('view-parent-login');
    const viewParentRegister = document.getElementById('view-parent-register');
    const viewParentDash = document.getElementById('view-parent-dash');
    const viewSetup = document.getElementById('view-setup');
    const viewDash = document.getElementById('view-dashboard');
    const viewLock = document.getElementById('view-locked');

    // DOM Elements - Navigation
    const btnRoleParent = document.getElementById('btn-role-parent');
    const btnRoleChild = document.getElementById('btn-role-child');
    const btnBackLogin = document.getElementById('btn-back-login');
    const btnBackRegister = document.getElementById('btn-back-register');
    const btnBackSetup = document.getElementById('btn-back-setup');
    const btnGoRegister = document.getElementById('btn-go-register');

    // DOM Elements - Auth
    const btnLogin = document.getElementById('btn-login');
    const btnRegister = document.getElementById('btn-register');
    const loginEmail = document.getElementById('login-email');
    const loginPassword = document.getElementById('login-password');
    const regName = document.getElementById('reg-name');
    const regEmail = document.getElementById('reg-email');
    const regPassword = document.getElementById('reg-password');
    const loginError = document.getElementById('login-error');
    const regError = document.getElementById('reg-error');
    const btnParentLogout = document.getElementById('btn-parent-logout');

    // DOM Elements - Parent Dash
    const parentNameEl = document.getElementById('parent-name');
    const parentChildList = document.getElementById('parent-child-list');
    const btnShowAddModal = document.getElementById('btn-show-add-modal');
    const modalAddDevice = document.getElementById('modal-add-device');
    const btnCloseModal = document.getElementById('btn-close-modal');
    const newDeviceCode = document.getElementById('new-device-code');
    const newDeviceTimer = document.getElementById('new-device-timer');

    // DOM Elements - Child Setup
    const setupEmail = document.getElementById('setup-email');
    const codeBoxes = document.querySelectorAll('.code-box');
    const btnVerify = document.getElementById('btn-verify');
    const loader = document.getElementById('setup-loader');

    // DOM Elements - Child Dash
    const clockEl = document.getElementById('clock');
    const timerMins = document.getElementById('timer-mins');
    const progress = document.getElementById('time-progress');
    const limitText = document.getElementById('limit-text');
    const locStatus = document.getElementById('location-status');
    const btnLoc = document.getElementById('btn-ping-location');
    const btnTime = document.getElementById('btn-request-time');
    const btnUnpair = document.getElementById('btn-unpair');
    const nameEl = document.getElementById('member-name');
    const avatarEl = document.getElementById('member-avatar');
    const appList = document.getElementById('app-list-section');
    const batStatus = document.getElementById('battery-status');

    // --- NAVIGATION HELPERS ---
    function hideAllViews() {
        document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
    }
    function showView(view) {
        hideAllViews();
        view.classList.add('active');
    }

    btnRoleParent.addEventListener('click', () => showView(viewParentLogin));
    btnRoleChild.addEventListener('click', () => showView(viewSetup));
    btnBackLogin.addEventListener('click', () => showView(viewRoleSelect));
    btnBackRegister.addEventListener('click', () => showView(viewParentLogin));
    btnBackSetup.addEventListener('click', () => showView(viewRoleSelect));
    btnGoRegister.addEventListener('click', () => showView(viewParentRegister));

    // --- INITIAL LOAD LOGIC ---
    if (parentId) {
        loadParentDashboard();
    } else if (memberId) {
        loadMember();
    } else {
        showView(viewRoleSelect);
    }

    // --- CLOCK & BATTERY ---
    setInterval(() => {
        const d = new Date();
        if (clockEl) clockEl.innerText = d.getHours().toString().padStart(2,'0') + ':' + d.getMinutes().toString().padStart(2,'0');
    }, 1000);

    if (navigator.getBattery && batStatus) {
        navigator.getBattery().then(b => {
            const update = () => { batStatus.innerText = Math.round(b.level*100)+'% '+(b.charging?'Şarj':''); };
            update(); b.addEventListener('levelchange', update); b.addEventListener('chargingchange', update);
        });
    }

    // --- PARENT AUTH ---
    btnLogin.addEventListener('click', async () => {
        loginError.style.display = 'none';
        const email = loginEmail.value.trim();
        const password = loginPassword.value;
        if (!email || !password) { loginError.innerText = "Lütfen alanları doldurun."; loginError.style.display = 'block'; return; }
        
        btnLogin.innerText = "Giriş yapılıyor...";
        try {
            const r = await fetch(API+'/auth/login', {
                method: 'POST', headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({ email, password })
            });
            if (!r.ok) throw new Error("E-posta veya şifre hatalı.");
            const user = await r.json();
            parentId = user.id;
            parentData = user;
            localStorage.setItem('fametra_pid', parentId);
            loginEmail.value = ''; loginPassword.value = '';
            loadParentDashboard();
        } catch (err) {
            loginError.innerText = err.message;
            loginError.style.display = 'block';
        } finally {
            btnLogin.innerText = "Giriş Yap";
        }
    });

    btnRegister.addEventListener('click', async () => {
        regError.style.display = 'none';
        const name = regName.value.trim();
        const email = regEmail.value.trim();
        const password = regPassword.value;
        if (!name || !email || !password) { regError.innerText = "Lütfen alanları doldurun."; regError.style.display = 'block'; return; }
        
        btnRegister.innerText = "Kayıt olunuyor...";
        try {
            const r = await fetch(API+'/auth/register', {
                method: 'POST', headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({ fullName: name, email, password })
            });
            if (!r.ok) {
                const text = await r.text();
                throw new Error(text || "Kayıt olurken bir hata oluştu.");
            }
            const user = await r.json();
            parentId = user.id;
            parentData = user;
            localStorage.setItem('fametra_pid', parentId);
            regName.value = ''; regEmail.value = ''; regPassword.value = '';
            loadParentDashboard();
        } catch (err) {
            regError.innerText = err.message;
            regError.style.display = 'block';
        } finally {
            btnRegister.innerText = "Kayıt Ol";
        }
    });

    btnParentLogout.addEventListener('click', () => {
        localStorage.removeItem('fametra_pid');
        parentId = null;
        parentData = null;
        showView(viewRoleSelect);
    });

    // --- PARENT DASHBOARD ---
    async function loadParentDashboard() {
        showView(viewParentDash);
        try {
            // Profil bilgisini çek (opsiyonel, login/register'dan geldiyse var)
            if (!parentData) {
                // Sadece çocuk listesini çekeceğiz, ismini bulamayabiliriz ama şimdilik "Ebeveyn" yazsın
                parentNameEl.innerText = "Yönetim";
            } else {
                parentNameEl.innerText = parentData.fullName.split(' ')[0];
            }

            // Çocukları getir
            const r = await fetch(API+'/members/by-user/'+parentId);
            if (!r.ok) throw new Error("Oturum düştü");
            const members = await r.json();
            
            parentChildList.innerHTML = '';
            if (members.length === 0) {
                parentChildList.innerHTML = `<div class="empty-state">Henüz eklenmiş bir cihaz yok.</div>`;
            } else {
                members.forEach(m => {
                    const el = document.createElement('div');
                    el.className = 'child-card';
                    el.innerHTML = `
                        <div class="avatar">${m.avatarEmoji || '👦'}</div>
                        <div class="child-card-info">
                            <h4>${m.name}</h4>
                            <p><i class="fa-solid fa-circle" style="color:${m.isOnline?'var(--success)':'var(--text3)'}"></i> ${m.deviceName || 'Cihaz'}</p>
                        </div>
                    `;
                    parentChildList.appendChild(el);
                });
            }

        } catch (err) {
            localStorage.removeItem('fametra_pid');
            parentId = null;
            showView(viewRoleSelect);
        }
    }

    btnShowAddModal.addEventListener('click', async () => {
        modalAddDevice.classList.add('active');
        newDeviceCode.innerText = "------";
        newDeviceTimer.innerText = "Kod oluşturuluyor...";
        
        try {
            const r = await fetch(API+'/members/pairing/generate', {
                method: 'POST', headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({ userId: parentId, name: 'Yeni Üye', avatarEmoji: '👦' })
            });
            if (!r.ok) throw new Error("Kod oluşturulamadı");
            const data = await r.json();
            
            newDeviceCode.innerText = data.code;
            
            let timeLeft = 30;
            newDeviceTimer.innerText = `Kalan süre: ${timeLeft} saniye`;
            if (generateCodeTimer) clearInterval(generateCodeTimer);
            
            generateCodeTimer = setInterval(() => {
                timeLeft--;
                if (timeLeft <= 0) {
                    clearInterval(generateCodeTimer);
                    newDeviceTimer.innerText = "Süre doldu, pencereyi kapatıp tekrar açın.";
                    newDeviceCode.innerText = "------";
                } else {
                    newDeviceTimer.innerText = `Kalan süre: ${timeLeft} saniye`;
                }
            }, 1000);

        } catch (err) {
            newDeviceTimer.innerText = "Hata oluştu.";
        }
    });

    btnCloseModal.addEventListener('click', () => {
        modalAddDevice.classList.remove('active');
        if (generateCodeTimer) clearInterval(generateCodeTimer);
        // Listeyi yenile
        loadParentDashboard();
    });

    // --- CHILD SETUP (PAIRING) ---
    // Code input auto-advance
    codeBoxes.forEach((box, i) => {
        box.addEventListener('input', () => { if (box.value && i < codeBoxes.length-1) codeBoxes[i+1].focus(); });
        box.addEventListener('keydown', e => { if (e.key==='Backspace' && !box.value && i>0) codeBoxes[i-1].focus(); });
    });

    btnVerify.addEventListener('click', async () => {
        const email = setupEmail.value.trim();
        const code = Array.from(codeBoxes).map(b=>b.value).join('');
        
        if (!email) { alert('Lütfen ebeveyn e-postasını girin.'); return; }
        if (code.length!==6) { shake(); return; }

        btnVerify.style.display='none';
        loader.classList.add('active');
        
        try {
            const r = await fetch(API+'/members/pairing/verify', {
                method:'POST', headers:{'Content-Type':'application/json'},
                body: JSON.stringify({email, code, deviceName: getDevice()})
            });
            if (!r.ok) { 
                const errText = await r.text();
                alert(errText || 'Kod geçersiz veya e-posta hatalı.'); 
                reset(); return; 
            }
            const m = await r.json();
            memberId = m.id;
            memberData = m;
            localStorage.setItem('fametra_mid', memberId);
            loader.classList.remove('active');
            showChildDash();
        } catch { alert('Sunucuya bağlanılamadı.'); reset(); }
    });

    async function loadMember() {
        try {
            const r = await fetch(API+'/members/'+memberId);
            if (!r.ok) { localStorage.removeItem('fametra_mid'); memberId=null; showView(viewRoleSelect); return; }
            memberData = await r.json();
            showChildDash();
        } catch { localStorage.removeItem('fametra_mid'); memberId=null; showView(viewRoleSelect); }
    }

    function showChildDash() {
        showView(viewDash);
        if (memberData) {
            nameEl.innerText = 'Merhaba, '+memberData.name;
            avatarEl.innerText = memberData.avatarEmoji||'👦';
            totalLimit = memberData.dailyScreenTimeLimitMinutes||120;
            timeLeft = Math.max(0, totalLimit-(memberData.usedScreenTimeMinutesToday||0));
            limitText.innerText = 'Günlük Limit: '+totalLimit+' dk';
            renderApps(memberData.appRestrictions||[]);
        }
        updateTimer();
        startTimer();
        setInterval(heartbeat, 8000);
        sendLoc();
        setInterval(sendLoc, 30000);
    }

    // --- APPS ---
    function renderApps(list) {
        const icons = {
            'YouTube':{bg:'#ff0000',ic:'fa-brands fa-youtube'},
            'Instagram':{bg:'#E1306C',ic:'fa-brands fa-instagram'},
            'TikTok':{bg:'#000000',ic:'fa-brands fa-tiktok'},
            'Facebook':{bg:'#1877f2',ic:'fa-brands fa-facebook'},
            'Oyunlar':{bg:'#0A84FF',ic:'fa-solid fa-gamepad'}
        };
        let h='<h3>Uygulama İzinleri</h3>';
        if (list.length === 0) h += '<p class="loading-text">Kısıtlama yok</p>';
        for (const a of list) {
            const ic = icons[a.appName]||{bg:'#636E72',ic:'fa-solid fa-mobile'};
            h+=`<div class="app-item">
                <div class="app-icon" style="background:${ic.bg}"><i class="${ic.ic}"></i></div>
                <div class="app-details"><h4>${a.appName}</h4><p>${a.isBlocked?'Engellendi':'Limit: '+a.dailyLimitMinutes+' dk'}</p></div>
                <div class="badge ${a.isBlocked?'blocked':'allowed'}">${a.isBlocked?'Kilitli':'Serbest'}</div>
            </div>`;
        }
        appList.innerHTML=h;
    }

    // --- TIMER ---
    function updateTimer() {
        timerMins.innerText = timeLeft;
        const pct = totalLimit>0?timeLeft/totalLimit:0;
        progress.style.strokeDashoffset = 283-(283*pct);
        progress.style.stroke = timeLeft<=15?'var(--danger)':'var(--primary)';
        timerMins.style.color = timeLeft<=15?'var(--danger)':'var(--text1)';
    }

    function startTimer() {
        if (timerInterval) clearInterval(timerInterval);
        timerInterval = setInterval(() => {
            if (timeLeft>0) { timeLeft--; updateTimer(); if(timeLeft%5===0) reportTime(); }
            else { clearInterval(timerInterval); reportTime(); lockDevice(); }
        }, 1000);
    }

    async function reportTime() {
        if (!memberId) return;
        try { await fetch(API+'/members/'+memberId+'/screentime',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({usedMinutes:totalLimit-timeLeft})}); } catch{}
    }

    function lockDevice() { showView(viewLock); }

    // --- HEARTBEAT ---
    async function heartbeat() {
        if (!memberId) return;
        try {
            const r = await fetch(API+'/members/'+memberId+'/heartbeat',{method:'POST'});
            if (!r.ok) return;
            const d = await r.json();
            if (d.dailyScreenTimeLimitMinutes!==totalLimit) {
                totalLimit=d.dailyScreenTimeLimitMinutes;
                timeLeft=Math.max(0,d.remainingMinutes);
                limitText.innerText='Günlük Limit: '+totalLimit+' dk';
                updateTimer();
                if (!d.isLocked && viewLock.classList.contains('active')) {
                    showView(viewDash); startTimer();
                }
            }
            if (d.restrictions) renderApps(d.restrictions);
            if (d.isLocked && !viewLock.classList.contains('active')) {
                clearInterval(timerInterval); timeLeft=0; updateTimer(); lockDevice();
            }
        } catch{}
    }

    // --- LOCATION ---
    async function sendLoc() {
        if (!memberId) return;
        locStatus.innerText='Gönderiliyor...';
        const send = async (lat,lng) => {
            try {
                await fetch(API+'/members/'+memberId+'/location',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({latitude:lat,longitude:lng,lastLocationText:lat.toFixed(4)+', '+lng.toFixed(4)})});
                locStatus.innerText='İletildi ✓';
            } catch { locStatus.innerText='Hata'; }
        };
        if (navigator.geolocation) {
            navigator.geolocation.getCurrentPosition(p=>send(p.coords.latitude,p.coords.longitude), ()=>send(41.0082,28.9784));
        } else send(41.0082,28.9784);
    }
    if (btnLoc) btnLoc.addEventListener('click', sendLoc);

    // --- EK SÜRE ---
    if (btnTime) {
        btnTime.addEventListener('click', async () => {
            btnTime.innerHTML='<i class="fa-solid fa-spinner fa-spin"></i> Bekleniyor...';
            btnTime.disabled=true;
            try {
                const r = await fetch(API+'/members/'+memberId+'/time-request',{method:'POST'});
                const req = await r.json();
                setTimeout(async()=>{
                    try {
                        const ar = await fetch(API+'/members/time-request/'+req.id+'/approve',{method:'POST'});
                        const d = await ar.json();
                        totalLimit=d.dailyScreenTimeLimitMinutes;
                        timeLeft=Math.max(0,d.remainingMinutes);
                        limitText.innerText='Günlük Limit: '+totalLimit+' dk';
                        showView(viewDash);
                        updateTimer(); startTimer();
                    } catch{}
                    btnTime.innerHTML='<i class="fa-solid fa-clock"></i> 15 Dk Ek Süre İste';
                    btnTime.disabled=false;
                },3000);
            } catch { btnTime.innerHTML='<i class="fa-solid fa-clock"></i> 15 Dk Ek Süre İste'; btnTime.disabled=false; }
        });
    }

    // --- UNPAIR ---
    if (btnUnpair) {
        btnUnpair.addEventListener('click', () => {
            if (confirm('Bağlantıyı kaldırmak istediğine emin misin?')) {
                localStorage.removeItem('fametra_mid');
                location.reload();
            }
        });
    }

    // --- HELPERS ---
    function shake() {
        if (!document.getElementById('ss')){const s=document.createElement('style');s.id='ss';s.innerHTML='@keyframes shake{0%,100%{transform:translateX(0)}25%{transform:translateX(-10px)}75%{transform:translateX(10px)}}';document.head.appendChild(s);}
        const c=document.querySelector('.code-input-container');c.style.animation='shake .5s';setTimeout(()=>c.style.animation='',500);
    }
    function reset() { loader.classList.remove('active'); btnVerify.style.display=''; }
    function getDevice() {
        const u=navigator.userAgent;
        if(/Android/i.test(u))return'Android';if(/iPhone/i.test(u))return'iPhone';return'Tarayıcı';
    }
});
