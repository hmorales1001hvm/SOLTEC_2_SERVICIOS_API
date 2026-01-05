$(document).ready(function () {
	$('#myTable').pageMe({
		pagerSelector: '#myPager',
		activeColor: 'blue',
		prevText: 'Anterior',
		nextText: 'Siguiente',
		showPrevNext: true,
		hidePageNumbers: false,
		perPage: 10
    });


    // PASO 1 ::: DETECTAR EL NAVEGADOR
    const elNavegadorEsCompatible = () => {
        if (navigator.userAgent.indexOf("Chrome") || navigator.userAgent.indexOf("Edge") || navigator.userAgent.indexOf("Safari")) return true;
        alert('El Navegador no es compatible con el Reconocimiento de voz');
        return false;
    }
    // PASO 2 ::: SI EL NAVEGADOR ES COMPATIBLE CONFIGURAR EL RECONOCIMIENTO DE VOZ
    if (elNavegadorEsCompatible()) {
        // 2.1 Esta api tiene nombres distintos según el navegador porque aún está en fase experimental, por eso las listamos todas e instanciamos la primera que consiga
        const recognition = new (window.SpeechRecognition || window.webkitSpeechRecognition || window.mozSpeechRecognition || window.msSpeechRecognition)();

        // 2.2 Definimos el idioma a escuchar https://en.wikipedia.org/wiki/Language_localisation#:~:text=Examples%20of%20language%20tags
        recognition.lang = "es-MX";

        // 2.3 Configuramos que cuando termine de reconocer algo vuelva a escuchar
        recognition.onend = event => { recognition.start(); };
        // 2.3 Pasamos la función que se llamará cuando haya un resultado del reconocimiento de voz
        recognition.onresult = resultado => { manejarResultado(resultado); };
        // 2.4 Empezamos a escuchar
        recognition.start();
    }

    // PASO 3 DEFINIMOS LA FUNCIÓN QUE MANEJARÁ RESULTADO DEL RECONOCIMIENTO DE VOZ
    const manejarResultado = resultado => {
        // 3.1 PINTAMOS LOS RESULTADOS EN EL HTML
        document.body.innerHTML = resultado.results[0][0].transcript
        // *******BONUS*******
        // Si el resultado es igual a 'abrir wikipedia' abriremos wikipedia
        if (resultado.results[0][0].transcript.toLowerCase().trim() == 'abrir') {
            const childFrame = document.createElement('iframe');
            childFrame.src = "https://es.wikipedia.org/wiki/Wikipedia:Portada";
            childFrame.style.width = "100vw";
            childFrame.style.height = "500px";
            document.body.append(childFrame)
        }
    }

});



$.getScript("http://localhost:8093/signalr/hubs")
    .done(

        function () {
            let guid = "";
            $.connection.hub.url = "http://localhost:8093/signalr";

            let blnkHub = $.connection.biolinkHub;

            //Suscrieb preview de firma
            blnkHub.client.showTest = function (str) {
                console.log(str);
            }

            //const recognition = new webkitSpeechRecognition(); // Para Chrome
            //recognition.continuous = true;
            //recognition.interimResults = false;

            //recognition.onresult = function (event) {
            //    debugger;
            //    const transcript = event.results[event.results.length - 1][0].transcript;
            //    console.log("Texto:", transcript);
            //    //connection.invoke("SendSpeechText", transcript);
            //    blnkHub.server.sendSpeechText(transcript);
            //};


            function detectedVoice() {
                var ses = new webkitSpeechRecognition();
                ses.interimResults = true; ``
                ses.maxAlternatives = 1;
                ses.continuous = true;
                ses.interimResults = true;
                ses.onstart = true;
                ses.onend = function () {
                    ses.start();
                };
                ses.onresult = function (e) {
                    if (event.results.length > 0) {
                        sonuc = event.results[event.results.length - 1];
                        if (sonuc.isFinal) {
                            var result = sonuc[0].transcript;
                            console.log(result);
                        }
                    }
                }
            }
       
            $.connection.hub.start().done(function () {
                //blnkHub.server.getTest(guid);

                //detectedVoice();
                //recognition.start();

            });


            let mimeType = 'audio/webm'; // Valor predeterminado

            if (MediaRecorder.isTypeSupported('audio/wav')) {
                mimeType = 'audio/wav';
            } else if (MediaRecorder.isTypeSupported('audio/ogg')) {
                mimeType = 'audio/ogg';
            } else if (MediaRecorder.isTypeSupported('audio/webm')) {
                mimeType = 'audio/webm';
            } else {
                console.error('No MIME type soportado encontrado');
            }

            console.log('Usando mimeType:', mimeType);




            blnkHub.client.sendAsync = function (text) {
                console.log("Texto:", text);
            }

            document.getElementById("startBtn").addEventListener("click", async () => {
                // Conectar a SignalR
                //connection = new signalR.HubConnectionBuilder()
                //    .withUrl("/audioHub")
                //    .build();

                //connection.on("ReceiveTranscription", text => {
                //    output.textContent += text + "\n";
                //});
                

                //await $.connection.hub.start();

                // Obtener el audio
                const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
                mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });

                mediaRecorder.start(3000); // graba cada 1 segundo
                console.log("Procesando info");
                mediaRecorder.ondataavailable = (e) => {
                    if (e.data.size > 0) {
                        e.data.arrayBuffer().then(buffer => {
                            const base64Audio = btoa(String.fromCharCode(...new Uint8Array(buffer)));
                            console.log("Invocando método: receiveAudio ");
                            //blnkHub.server.receiveAudio(base64Audio);
                            blnkHub.server.sendAudio(base64Audio);
                            
                        });
                    }
                };

                document.getElementById("startBtn").disabled = true;
                document.getElementById("stopBtn").disabled = false;
            });




            //navigator.mediaDevices.getUserMedia({ audio: true }).then(stream => {
            //    const mediaRecorder = new MediaRecorder(stream);
            //    mediaRecorder.start(250);

            //    mediaRecorder.ondataavailable = e => {
            //        if ($.connection.state === "Connected") {
            //            e.data.arrayBuffer().then(buffer => {
            //                const base64Audio = btoa(String.fromCharCode(...new Uint8Array(buffer)));
            //                $.connection.server.receiveAudio(base64Audio);
            //            });
            //        }
            //    };
            //});

                      

            //blnkHub.client.ReceiveTranscription = function (text) {
            //    console.log("Texto:", text);
            //}

        }


        

    ).fail(function (jqxhr, settings, exception) {

        FailConnectServiceMessage();

    });